namespace Birel_Plaka;

/// <summary>Limits a group of reads must meet before its plate is decided ([Consensus] in camera-settings.ini).</summary>
public sealed record ConsensusRules(int MinReads, double MinShare, int MinCover, int DissentReads, double DissentFraction);

/// <summary>Why a group's plate was not decided (UI texts in <see cref="ConsensusText"/>).</summary>
public enum UndecidedReason
{
    None,
    /// <summary>Fewer reads than MinReads.</summary>
    TooFewReads,
    /// <summary>The vote left no character.</summary>
    NothingRead,
    /// <summary>More than half of the reads touch the same edge of the detection area.</summary>
    CutOff,
    /// <summary>A character was covered by fewer than MinCover reads.</summary>
    TooFewCover,
    /// <summary>A character's winner has less than MinShare of the vote weight.</summary>
    Disagree,
    /// <summary>A losing answer for a character is backed by too many reads.</summary>
    Dissent,
    /// <summary>Too many reads did not fit the group's plate at all (probably another car).</summary>
    DissentingReads,
    /// <summary>The decided text matches none of the enabled plate formats.</summary>
    Format,
    /// <summary>The OCR's country estimate does not favour an enabled format's country.</summary>
    Region
}

/// <summary>One answer in a column: a character (alphabet index) or -1 = no character here.</summary>
public sealed record ConsensusOption(int Symbol, double Weight, int Reads);

/// <summary>One position of the aligned reads and how the reads voted there.</summary>
public sealed class ConsensusColumn
{
    internal ConsensusColumn(bool insertion, IReadOnlyList<ConsensusOption> options, int covered, float[]? profile)
    {
        Insertion = insertion;
        Options = options;
        Covered = covered;
        Profile = profile;
        Root = profile == null ? null : PlateConsensus.Roots(profile);

        double total = options.Sum(o => o.Weight);
        Winner = options.Count == 0 ? -1 : options[0].Symbol;
        Share = total > 0 && options.Count > 0 ? options[0].Weight / total : 0;
    }

    /// <summary>Between two characters of the reference: only some reads had an extra character here.</summary>
    public bool Insertion { get; }

    /// <summary>The answers, strongest first.</summary>
    public IReadOnlyList<ConsensusOption> Options { get; }

    /// <summary>Winning answer: alphabet index, or -1 when "no character" won.</summary>
    public int Winner { get; }

    /// <summary>The winner's share of the vote weight (0..1).</summary>
    public double Share { get; }

    /// <summary>Reads that reached this position (cut-off ends do not).</summary>
    public int Covered { get; }

    /// <summary>Mean probability vector of the reads that voted for the winning character (null for "no character").</summary>
    public float[]? Profile { get; }

    internal float[]? Root { get; }

    /// <summary>1-based position in the voted text this column belongs to (for messages).</summary>
    public int Position { get; internal set; }
}

/// <summary>The voted plate of a set of reads, with every column's vote and how each read fits.</summary>
public sealed class ConsensusResult
{
    internal ConsensusResult(string text, IReadOnlyList<ConsensusColumn> columns, IReadOnlyList<ReadFit> fits, int reference)
    {
        Text = text;
        Columns = columns;
        Fits = fits;
        Reference = reference;
    }

    /// <summary>The voted characters (may be empty).</summary>
    public string Text { get; }

    public IReadOnlyList<ConsensusColumn> Columns { get; }

    /// <summary>Per read (same order as the input): how it aligns to <see cref="Text"/>.</summary>
    public IReadOnlyList<ReadFit> Fits { get; }

    /// <summary>Index of the medoid read the first alignment was made against.</summary>
    public int Reference { get; }

    /// <summary>Lowest winner share over all columns (1 when there are none).</summary>
    public double LowestShare => Columns.Count == 0 ? 1 : Columns.Min(c => c.Share);

    /// <summary>
    /// The read whose frame best shows the decided plate: among reads that agree with the voted text
    /// character for character, the one with the highest weakest-character confidence; reads that
    /// cover the whole plate first. -1 when no read agrees.
    /// </summary>
    public int BestRead(IReadOnlyList<PlateRead> reads)
    {
        int best = -1;
        for (int i = 0; i < Fits.Count; i++)
        {
            if (!Fits[i].Agrees)
                continue;
            if (best < 0
                || (Fits[i].Complete && !Fits[best].Complete)
                || (Fits[i].Complete == Fits[best].Complete && reads[i].WeakestConfidence > reads[best].WeakestConfidence))
                best = i;
        }
        return best;
    }
}

/// <summary>How one read lines up with the voted text.</summary>
/// <param name="Cost">Alignment cost (character units; missing ends are free).</param>
/// <param name="Agrees">Every character it has equals the voted one, nothing extra, nothing missing in between.</param>
/// <param name="Complete">It agrees and covers the whole voted text.</param>
public readonly record struct ReadFit(double Cost, bool Agrees, bool Complete);

/// <summary>
/// Votes the plate of one car from several OCR reads, without knowing any plate format:
///
///  1. Characters are compared by the model's own probability vectors:
///     cost(a,b) = 1 - sum_k sqrt(pa[k] * pb[k])  (0 = surely the same character, 1 = surely different).
///  2. Reads are aligned with an edit distance where characters missing at the START or END of a read
///     are free (the plate box may be too tight or the plate cut off) and every other gap costs 1.
///  3. The reference is the medoid read (lowest total cost of aligning all other reads to it). Every read
///     is aligned to it; characters a read has between reference positions become insertion columns.
///  4. Each column is voted, weighted by each character's confidence; a read that skipped the character
///     votes "no character" with its median confidence; reads that did not reach a column do not vote.
///  5. All reads are re-aligned to the voted text and voted again (two rounds).
///
/// Add reads with <see cref="Add"/>; pairwise costs are kept, so a new read costs one alignment per
/// existing read. Not thread-safe; one instance per group.
/// </summary>
public sealed class PlateConsensus
{
    private const double GapCost = 1.0;

    private readonly string _alphabet;
    private readonly List<PlateRead> _reads = new();

    // _costs[r][q] = cost of aligning read q onto read r as the reference.
    private readonly List<List<double>> _costs = new();

    public PlateConsensus(string alphabet)
    {
        _alphabet = alphabet;
    }

    public IReadOnlyList<PlateRead> Reads => _reads;

    public void Add(PlateRead read)
    {
        var refRow = new List<double>(_reads.Count + 1);
        for (int r = 0; r < _reads.Count; r++)
        {
            _costs[r].Add(Align(read, RootsOf(_reads[r])).Cost);        // new read onto old reference
            refRow.Add(Align(_reads[r], RootsOf(read)).Cost);           // old read onto the new one
        }
        refRow.Add(0);
        _reads.Add(read);
        _costs.Add(refRow);
    }

    /// <summary>Votes the plate of the reads added so far. Null when there are none.</summary>
    public ConsensusResult? Build()
    {
        if (_reads.Count == 0)
            return null;

        int reference = Medoid();
        var round1 = Vote(RootsOf(_reads[reference]));
        float[][] profile1 = round1.Roots;

        ConsensusResult result;
        if (profile1.Length == 0)
        {
            result = Finish(round1, reference);
        }
        else
        {
            var round2 = Vote(profile1);
            result = Finish(round2, reference);
        }

        return result;
    }

    /// <summary>Alignment cost of a read against a voted text (missing ends free, in character units).</summary>
    public static double Cost(PlateRead read, ConsensusResult result) => Align(read, ProfileRoots(result)).Cost;

    private static float[][] ProfileRoots(ConsensusResult result) =>
        result.Columns.Where(c => c.Winner >= 0).Select(c => c.Root!).ToArray();

    // ------------------------------------------------------------------ cost and alignment

    /// <summary>1 - Bhattacharyya coefficient of two probability vectors (0 same .. 1 different).</summary>
    public static double CharCost(float[] a, float[] b) => RootCost(Roots(a), Roots(b));

    /// <summary>Element-wise square roots of a probability vector.</summary>
    public static float[] Roots(float[] p)
    {
        var r = new float[p.Length];
        for (int k = 0; k < p.Length; k++)
            r[k] = MathF.Sqrt(Math.Max(0f, p[k]));
        return r;
    }

    /// <summary><see cref="CharCost"/> from square-rooted vectors: 1 - their dot product.</summary>
    private static double RootCost(float[] a, float[] b)
    {
        float bc = 0;
        for (int k = 0; k < a.Length; k++)
            bc += a[k] * b[k];
        return Math.Clamp(1 - bc, 0, 1);
    }

    private static float[][] RootsOf(PlateRead read)
    {
        var v = new float[read.Length][];
        for (int i = 0; i < read.Length; i++)
            v[i] = read.Roots(i);
        return v;
    }

    /// <summary>Where each of a read's characters went and which reference columns it spans.</summary>
    internal sealed class Alignment
    {
        public double Cost;
        /// <summary>Per read character: the reference column it is aligned to, or -1 when it is an insertion.</summary>
        public int[] Column = Array.Empty<int>();
        /// <summary>Per read character that is an insertion: the slot (it lies before reference column Slot).</summary>
        public int[] Slot = Array.Empty<int>();
        /// <summary>First / last reference column aligned to a character; First = -1 when none.</summary>
        public int First = -1, Last = -1;
    }

    /// <summary>
    /// Semi-global alignment of a read onto a reference (given as square-rooted probability vectors):
    /// reference characters before the read's first and after its last character are skipped for free
    /// (the read is a cut-off piece of the plate); every other gap costs 1, a substitution costs
    /// <see cref="CharCost"/>.
    /// </summary>
    internal static Alignment Align(PlateRead read, IReadOnlyList<float[]> reference)
    {
        int m = read.Length, n = reference.Count;
        var d = new double[m + 1, n + 1];
        var step = new byte[m + 1, n + 1];   // 1 = substitution, 2 = read char inserted, 3 = reference char skipped

        for (int j = 0; j <= n; j++) d[0, j] = 0;   // free start: the read may begin anywhere
        for (int i = 1; i <= m; i++) { d[i, 0] = i * GapCost; step[i, 0] = 2; }

        for (int i = 1; i <= m; i++)
        {
            float[] p = read.Roots(i - 1);
            for (int j = 1; j <= n; j++)
            {
                double sub = d[i - 1, j - 1] + RootCost(p, reference[j - 1]);
                double ins = d[i - 1, j] + GapCost;
                double del = d[i, j - 1] + GapCost;

                // Ties: substitution first, then insertion; keeps alignments deterministic.
                if (sub <= ins && sub <= del) { d[i, j] = sub; step[i, j] = 1; }
                else if (ins <= del) { d[i, j] = ins; step[i, j] = 2; }
                else { d[i, j] = del; step[i, j] = 3; }
            }
        }

        // Free end: the read may stop before the reference's last character.
        int endJ = 0;
        for (int j = 1; j <= n; j++)
            if (d[m, j] < d[m, endJ] - 1e-12) endJ = j;

        var a = new Alignment
        {
            Cost = d[m, endJ],
            Column = new int[m],
            Slot = new int[m]
        };

        int ii = m, jj = endJ;
        while (ii > 0)
        {
            byte s = jj == 0 ? (byte)2 : step[ii, jj];
            if (s == 1)
            {
                a.Column[ii - 1] = jj - 1;
                a.Slot[ii - 1] = -1;
                ii--; jj--;
            }
            else if (s == 2)
            {
                a.Column[ii - 1] = -1;
                a.Slot[ii - 1] = jj;
                ii--;
            }
            else
            {
                jj--;
            }
        }

        foreach (int c in a.Column)
        {
            if (c < 0) continue;
            if (a.First < 0 || c < a.First) a.First = c;
            if (c > a.Last) a.Last = c;
        }

        return a;
    }

    // ------------------------------------------------------------------ medoid and vote

    private int Medoid()
    {
        int best = 0;
        double bestTotal = double.MaxValue;
        for (int r = 0; r < _reads.Count; r++)
        {
            double total = 0;
            for (int q = 0; q < _reads.Count; q++)
                total += _costs[r][q];

            // Ties: the read with the higher mean confidence is the better reference.
            if (total < bestTotal - 1e-9
                || (Math.Abs(total - bestTotal) <= 1e-9 && _reads[r].Ocr.Confidence > _reads[best].Ocr.Confidence))
            {
                best = r;
                bestTotal = total;
            }
        }
        return best;
    }

    private sealed class Round
    {
        public required List<ConsensusColumn> Columns;
        /// <summary>Square-rooted profile vectors of the voted text.</summary>
        public required float[][] Roots;
        public required string Text;
    }

    /// <summary>Aligns every read to the reference and votes each column (reference columns and insertion slots).</summary>
    private Round Vote(IReadOnlyList<float[]> reference)
    {
        int n = reference.Count;
        var alignments = _reads.Select(r => Align(r, reference)).ToList();

        // How many extra characters any read has in each slot (slot k lies before reference column k).
        var slotDepth = new int[n + 1];
        foreach (var a in alignments)
        {
            var perSlot = new int[n + 1];
            foreach (int s in a.Slot)
                if (s >= 0) perSlot[s]++;
            for (int k = 0; k <= n; k++)
                slotDepth[k] = Math.Max(slotDepth[k], perSlot[k]);
        }

        var columns = new List<ConsensusColumn>();
        for (int k = 0; k <= n; k++)
        {
            for (int t = 0; t < slotDepth[k]; t++)
                columns.Add(VoteInsertion(alignments, k, t, n));
            if (k < n)
                columns.Add(VoteColumn(alignments, k));
        }

        var text = new System.Text.StringBuilder();
        var roots = new List<float[]>();
        foreach (var c in columns)
        {
            c.Position = Math.Max(1, text.Length + (c.Winner >= 0 ? 1 : 0));
            if (c.Winner < 0) continue;
            text.Append(_alphabet[c.Winner]);
            roots.Add(c.Root!);
        }

        // A column voted before the first character points at character 1.
        foreach (var c in columns)
            c.Position = Math.Clamp(c.Position, 1, Math.Max(1, text.Length));

        return new Round { Columns = columns, Roots = roots.ToArray(), Text = text.ToString() };
    }

    private ConsensusColumn VoteColumn(List<Alignment> alignments, int col)
    {
        var votes = new Votes(_alphabet.Length);

        for (int r = 0; r < _reads.Count; r++)
        {
            var a = alignments[r];
            if (a.First < 0 || col < a.First || col > a.Last)
                continue;   // the read did not reach this column

            int i = Array.IndexOf(a.Column, col);
            if (i >= 0)
                votes.Character(_reads[r], i);
            else
                votes.Nothing(_reads[r].MedianConfidence);
        }

        return votes.ToColumn(insertion: false);
    }

    /// <summary>The t-th extra character in slot k: reads that span the slot but have no such character vote "nothing".</summary>
    private ConsensusColumn VoteInsertion(List<Alignment> alignments, int slot, int t, int n)
    {
        var votes = new Votes(_alphabet.Length);

        for (int r = 0; r < _reads.Count; r++)
        {
            var a = alignments[r];
            if (a.First < 0)
                continue;

            // Spans the slot: covers both neighbouring columns; at either end of the reference,
            // reaching the end column is enough (the read saw that end of the plate).
            bool spans = slot == 0 ? a.First == 0
                : slot == n ? a.Last == n - 1
                : a.First <= slot - 1 && a.Last >= slot;

            int found = -1, seen = 0;
            for (int i = 0; i < a.Slot.Length; i++)
            {
                if (a.Slot[i] != slot) continue;
                if (seen++ == t) { found = i; break; }
            }

            if (found >= 0)
                votes.Character(_reads[r], found);
            else if (spans)
                votes.Nothing(_reads[r].MedianConfidence);
        }

        return votes.ToColumn(insertion: true);
    }

    private sealed class Votes
    {
        private readonly int _classes;
        private readonly Dictionary<int, (double Weight, int Reads)> _options = new();
        private readonly Dictionary<int, double[]> _sums = new();
        private int _covered;

        public Votes(int classes) => _classes = classes;

        public void Character(PlateRead read, int i)
        {
            int symbol = read.Symbol(i);
            Add(symbol, read.Confidence(i));

            if (!_sums.TryGetValue(symbol, out var sum))
                _sums[symbol] = sum = new double[_classes];
            float[] p = read.Probabilities(i);
            for (int k = 0; k < _classes; k++)
                sum[k] += p[k];
        }

        public void Nothing(float weight) => Add(-1, weight);

        private void Add(int symbol, double weight)
        {
            _covered++;
            _options.TryGetValue(symbol, out var o);
            _options[symbol] = (o.Weight + weight, o.Reads + 1);
        }

        public ConsensusColumn ToColumn(bool insertion)
        {
            // Strongest first; ties: more reads, then "no character", then the lower alphabet index.
            var options = _options
                .Select(kv => new ConsensusOption(kv.Key, kv.Value.Weight, kv.Value.Reads))
                .OrderByDescending(o => o.Weight).ThenByDescending(o => o.Reads).ThenBy(o => o.Symbol)
                .ToList();

            float[]? profile = null;
            if (options.Count > 0 && options[0].Symbol >= 0)
            {
                double[] sum = _sums[options[0].Symbol];
                double total = sum.Sum();
                profile = sum.Select(v => (float)(v / total)).ToArray();
            }

            return new ConsensusColumn(insertion, options, _covered, profile);
        }
    }

    private ConsensusResult Finish(Round round, int reference)
    {
        var fits = new List<ReadFit>(_reads.Count);
        int n = round.Roots.Length;

        foreach (var read in _reads)
        {
            if (n == 0)
            {
                fits.Add(new ReadFit(read.Length * GapCost, false, false));
                continue;
            }

            var a = Align(read, round.Roots);
            bool agrees = a.First >= 0 && read.Length > 0;
            int previous = -1;

            for (int i = 0; agrees && i < read.Length; i++)
            {
                int c = a.Column[i];
                agrees = c >= 0
                         && (previous < 0 || c == previous + 1)
                         && read.Symbol(i) == _alphabet.IndexOf(round.Text[c]);
                previous = c;
            }

            fits.Add(new ReadFit(a.Cost, agrees, agrees && a.First == 0 && a.Last == n - 1));
        }

        return new ConsensusResult(round.Text, round.Columns, fits, reference);
    }

    // ------------------------------------------------------------------ decision

    /// <summary>
    /// The column rules: every column covered by MinCover reads, its winner at MinShare of the weight,
    /// and no losing answer backed by DissentReads reads that are also DissentFraction of the column's
    /// reads. Returns the first broken rule and its 1-based character position, or None.
    /// </summary>
    public static (UndecidedReason Reason, int Position) CheckColumns(ConsensusResult result, ConsensusRules rules)
    {
        if (result.Text.Length == 0)
            return (UndecidedReason.NothingRead, 0);

        foreach (var c in result.Columns)
        {
            if (c.Covered < rules.MinCover)
                return (UndecidedReason.TooFewCover, c.Position);

            if (c.Share < rules.MinShare)
                return (UndecidedReason.Disagree, c.Position);

            foreach (var loser in c.Options.Skip(1))
            {
                if (loser.Reads >= rules.DissentReads && loser.Reads >= rules.DissentFraction * c.Covered)
                    return (UndecidedReason.Dissent, c.Position);
            }
        }

        return (UndecidedReason.None, 0);
    }
}
