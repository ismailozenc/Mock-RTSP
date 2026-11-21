namespace Birel_Plaka;

/// <summary>What a camera's read groups need from the settings ([Consensus], formats, sliders).</summary>
/// <param name="PassGapSeconds">A group closes when its car had no read for this long ([Travel] TrackTimeoutSeconds).</param>
/// <param name="MaxSeconds">A group closes at the latest this long after its first read ([Consensus] GroupMaxSeconds).</param>
/// <param name="JoinMaxCost">A read that costs more than this to align to the group's plate is recorded as dissent.</param>
/// <param name="MinDetection">Current minimum box confidence (0..1) for a read to take part (slider).</param>
/// <param name="FormatCheck">Null when the decided text is a valid plate, otherwise why not.</param>
/// <param name="RegionCheck">Null when off; otherwise checks the mean country probabilities (null = fine, else why not).</param>
public sealed record GroupPolicy(
    ConsensusRules Rules,
    double PassGapSeconds,
    double MaxSeconds,
    double JoinMaxCost,
    int PhotoCandidates,
    Func<float> MinDetection,
    Func<string, string?> FormatCheck,
    Func<float[], string?>? RegionCheck);

public enum GroupState
{
    /// <summary>The car is still being read.</summary>
    Reading,
    /// <summary>Closed and the reads clearly agree: one plate is saved.</summary>
    Decided,
    /// <summary>Closed without a reliable plate: nothing is saved ("unconfirmed").</summary>
    Undecided
}

/// <summary>What happened to one read of a group.</summary>
public enum ReadRole
{
    /// <summary>Takes part in the vote.</summary>
    Vote,
    /// <summary>Did not fit the group's plate (cost above JoinMaxCost): kept apart, probably another car.</summary>
    Dissent,
    /// <summary>Box confidence below the current minimum: does not vote.</summary>
    BelowMinimum
}

public readonly record struct GroupRead(PlateRead Read, ReadRole Role);

/// <summary>
/// A copy of a group's state for other threads (the UI, the database save, diagnostics). Immutable.
/// A closed group's <see cref="Photo"/> belongs to whoever receives it: release it when done.
/// </summary>
public sealed record GroupView(
    int Id,
    object TrackKey,
    GroupState State,
    string Text,
    UndecidedReason Reason,
    int ReasonPosition,
    string? ReasonDetail,
    IReadOnlyList<GroupRead> Reads,
    ConsensusResult? Consensus,
    PlateRead? BestRead,
    DateTime StartUtc,
    DateTime LastUtc)
{
    public int Votes => Reads.Count(r => r.Role == ReadRole.Vote);
    public int Dissent => Reads.Count(r => r.Role == ReadRole.Dissent);
    public int BelowMinimum => Reads.Count(r => r.Role == ReadRole.BelowMinimum);

    /// <summary>Frame for the vehicle image (closed groups only, may be null).</summary>
    public IFrameHandle? Photo { get; init; }
}

/// <summary>
/// Groups one camera's reads per car and decides each car's plate when it has left the picture.
/// A group is the reads of one <see cref="TravelTrack"/> (the same plate box followed across frames);
/// it closes when the track had no read for the pass gap or the group reached its maximum duration.
/// Used by the camera's detection thread only; hands immutable <see cref="GroupView"/>s to others.
/// </summary>
public sealed class ReadGroups
{
    private static int _nextId;

    private readonly GroupPolicy _policy;
    private readonly string _alphabet;
    private readonly long _gapTicks, _maxTicks;
    private readonly Dictionary<object, Group> _open = new();

    public ReadGroups(GroupPolicy policy, string alphabet, double ticksPerSecond)
    {
        if (policy.PhotoCandidates < 1)
            throw new ArgumentOutOfRangeException(nameof(policy), "At least one photo candidate is needed.");

        _policy = policy;
        _alphabet = alphabet;
        _gapTicks = (long)(policy.PassGapSeconds * ticksPerSecond);
        _maxTicks = (long)(policy.MaxSeconds * ticksPerSecond);
    }

    public int OpenGroups => _open.Count;

    private sealed class Group
    {
        public required int Id;
        public required object Key;
        public required PlateConsensus Consensus;
        public long Start, Last;
        public DateTime StartUtc, LastUtc;
        public readonly List<GroupRead> Reads = new();
        public ConsensusResult? Current;
        public readonly List<(PlateRead Read, int Vote, IFrameHandle Frame)> Candidates = new();
    }

    /// <summary>
    /// Adds a read to its car's group. <paramref name="takeFrame"/> is called only if the read becomes
    /// a photo candidate (it must still be valid then: call during the detection cycle). Returns the
    /// group's state after the read, and every group that had to close first (same car past its
    /// maximum duration, other cars past their pass gap).
    /// </summary>
    public (GroupView Group, List<GroupView> Closed) Add(PlateRead read, Func<IFrameHandle?> takeFrame)
    {
        object key = read.TrackKey ?? throw new ArgumentException("A read needs its track.", nameof(read));
        var closed = CloseExpired(read.Time);

        if (_open.TryGetValue(key, out var g) && read.Time - g.Start > _maxTicks)
        {
            closed.Add(Close(g));
            _open.Remove(key);
            g = null;
        }

        if (g == null)
        {
            g = new Group
            {
                Id = Interlocked.Increment(ref _nextId),
                Key = key,
                Consensus = new PlateConsensus(_alphabet),
                Start = read.Time,
                StartUtc = read.TimeUtc
            };
            _open[key] = g;
        }

        g.Last = read.Time;
        g.LastUtc = read.TimeUtc;

        ReadRole role;
        if (read.DetectionConfidence < _policy.MinDetection())
        {
            role = ReadRole.BelowMinimum;
        }
        else if (g.Consensus.Reads.Count >= _policy.Rules.MinReads
                 && g.Current is { Text.Length: > 0 } current
                 && PlateConsensus.Cost(read, current) > _policy.JoinMaxCost)
        {
            role = ReadRole.Dissent;   // most likely another car on the same spot: do not mix it in
        }
        else
        {
            role = ReadRole.Vote;
            g.Consensus.Add(read);
            g.Current = g.Consensus.Build();
            ConsiderPhoto(g, read, g.Consensus.Reads.Count - 1, takeFrame);
        }

        g.Reads.Add(new GroupRead(read, role));
        return (View(g, GroupState.Reading), closed);
    }

    /// <summary>Closes the groups whose car has not been read for the pass gap or that reached their maximum duration.</summary>
    public List<GroupView> CloseExpired(long now)
    {
        var closed = new List<GroupView>();
        foreach (var g in _open.Values.Where(g => now - g.Last > _gapTicks || now - g.Start > _maxTicks).OrderBy(g => g.Start).ToList())
        {
            _open.Remove(g.Key);
            closed.Add(Close(g));
        }
        return closed;
    }

    /// <summary>Closes every open group (camera stopping).</summary>
    public List<GroupView> CloseAll()
    {
        var closed = _open.Values.OrderBy(g => g.Start).Select(Close).ToList();
        _open.Clear();
        return closed;
    }

    // ------------------------------------------------------------------ photo candidates

    /// <summary>
    /// Keeps the frames of the few reads most likely to show the decided plate: reads that agree with
    /// the current vote (whole plate first), then the highest weakest-character confidence.
    /// </summary>
    private void ConsiderPhoto(Group g, PlateRead read, int vote, Func<IFrameHandle?> takeFrame)
    {
        var fits = g.Current!.Fits;
        (int, int, float) Score(PlateRead r, int v) =>
            (fits[v].Agrees ? 1 : 0, fits[v].Complete ? 1 : 0, r.WeakestConfidence);

        var score = Score(read, vote);

        if (g.Candidates.Count >= _policy.PhotoCandidates)
        {
            int worst = 0;
            for (int i = 1; i < g.Candidates.Count; i++)
                if (Score(g.Candidates[i].Read, g.Candidates[i].Vote).CompareTo(Score(g.Candidates[worst].Read, g.Candidates[worst].Vote)) < 0)
                    worst = i;

            var w = g.Candidates[worst];
            if (score.CompareTo(Score(w.Read, w.Vote)) <= 0)
                return;

            w.Frame.Release();
            g.Candidates.RemoveAt(worst);
        }

        IFrameHandle? frame = takeFrame();
        if (frame != null)
            g.Candidates.Add((read, vote, frame));
    }

    // ------------------------------------------------------------------ decision

    private GroupView Close(Group g)
    {
        var rules = _policy.Rules;
        var votes = g.Consensus.Reads;
        ConsensusResult? result = g.Consensus.Build();

        var joined = g.Reads.Where(r => r.Role != ReadRole.BelowMinimum).Select(r => r.Read).ToList();
        int dissent = g.Reads.Count(r => r.Role == ReadRole.Dissent);

        UndecidedReason reason = UndecidedReason.None;
        int position = 0;
        string? detail = null;

        if (votes.Count < rules.MinReads || result == null)
        {
            reason = UndecidedReason.TooFewReads;
        }
        else if (result.Text.Length == 0)
        {
            reason = UndecidedReason.NothingRead;
        }
        else if (joined.Count(r => r.TouchesLeft) * 2 > joined.Count || joined.Count(r => r.TouchesRight) * 2 > joined.Count)
        {
            reason = UndecidedReason.CutOff;
        }
        else
        {
            (reason, position) = PlateConsensus.CheckColumns(result, rules);

            if (reason == UndecidedReason.None
                && dissent >= rules.DissentReads && dissent >= rules.DissentFraction * joined.Count)
            {
                reason = UndecidedReason.DissentingReads;
            }

            if (reason == UndecidedReason.None && _policy.FormatCheck(result.Text) is { } formatProblem)
            {
                reason = UndecidedReason.Format;
                detail = formatProblem;
            }

            if (reason == UndecidedReason.None && _policy.RegionCheck is { } regionCheck)
            {
                // Country estimate of the reads that agree with the plate (all votes when none does).
                var agreeing = votes.Where((_, i) => result.Fits[i].Agrees).ToList();
                var source = (agreeing.Count > 0 ? agreeing : votes.ToList()).Where(r => r.Ocr.Regions != null).ToList();
                string? regionProblem = source.Count == 0
                    ? "the OCR model gave no country estimate"
                    : regionCheck(MeanRegions(source));
                if (regionProblem != null)
                {
                    reason = UndecidedReason.Region;
                    detail = regionProblem;
                }
            }
        }

        int best = result?.BestRead(votes) ?? -1;
        PlateRead? bestRead = best >= 0 ? votes[best] : null;

        // Photo: the candidate frame of the best read; otherwise the strongest candidate (an
        // unconfirmed group still needs a picture for the operator).
        IFrameHandle? photo = null;
        if (g.Candidates.Count > 0)
        {
            var chosen = g.Candidates.FirstOrDefault(c => ReferenceEquals(c.Read, bestRead));
            if (chosen.Frame == null)
            {
                chosen = g.Candidates
                    .OrderByDescending(c => result != null && result.Fits[c.Vote].Agrees)
                    .ThenByDescending(c => c.Read.WeakestConfidence)
                    .First();
                bestRead ??= chosen.Read;
            }

            photo = chosen.Frame;
            foreach (var c in g.Candidates)
                if (!ReferenceEquals(c.Frame, photo))
                    c.Frame.Release();
            g.Candidates.Clear();
        }

        g.Current = result;
        var state = reason == UndecidedReason.None ? GroupState.Decided : GroupState.Undecided;
        return View(g, state, reason, position, detail, bestRead) with { Photo = photo };
    }

    private static float[] MeanRegions(List<PlateRead> reads)
    {
        var mean = new float[reads[0].Ocr.Regions!.Length];
        foreach (var r in reads)
            for (int k = 0; k < mean.Length; k++)
                mean[k] += r.Ocr.Regions![k] / reads.Count;
        return mean;
    }

    private static GroupView View(Group g, GroupState state, UndecidedReason reason = UndecidedReason.None,
        int position = 0, string? detail = null, PlateRead? best = null) =>
        new(g.Id, g.Key, state, g.Current?.Text ?? "", reason, position, detail, g.Reads.ToArray(), g.Current,
            best, g.StartUtc, g.LastUtc);
}
