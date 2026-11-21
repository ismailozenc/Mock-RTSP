using System.Globalization;
using System.Text.RegularExpressions;

namespace MockRtsp;

public enum AppLanguage { English, Turkish }

/// <summary>
/// English and Turkish text for the scanner (and the shared controls it shows). The English text is
/// the key, so code and designers keep writing plain English: <see cref="Apply"/> walks a window and
/// swaps every known text for the chosen language, <see cref="T"/> translates text built at run time,
/// and <see cref="Translate"/> also recognises a filled-in template ("3 gap(s) picked...") in either
/// language, so a text made earlier follows a change of language. Text not in the table (file names,
/// plates, what the user types, log lines) is left alone, and so is everything that is saved
/// ("coming"/"going" in the files, CSV). The language is chosen per process: the RTSP app never
/// sets it, so it stays English.
/// </summary>
public static class Loc
{
    // Static fields are set in this order: the pattern for {0}... first, the table, then the templates made from both.
    private static readonly Regex Placeholder = new(@"\\\{(\d+)(?::[^}]*)?\}", RegexOptions.Compiled);
    private static readonly Dictionary<string, string> TurkishByEnglish = Build();
    private static readonly Dictionary<string, string> EnglishByTurkish =
        TurkishByEnglish.GroupBy(p => p.Value).ToDictionary(g => g.Key, g => g.First().Key);
    private static readonly List<(Regex English, Regex Turkish, string EnglishKey, string TurkishText)> Templates = BuildTemplates();

    /// <summary>Native names for the language combobox, in <see cref="AppLanguage"/> order.</summary>
    public static readonly string[] Names = { "English", "Türkçe" };

    public static AppLanguage Current { get; private set; } = AppLanguage.English;

    /// <summary>The language of Windows, for a first run: Turkish when Windows is in Turkish.</summary>
    public static AppLanguage FromWindows() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tr" ? AppLanguage.Turkish : AppLanguage.English;

    public static AppLanguage Parse(string? code) =>
        string.Equals(code, "tr", StringComparison.OrdinalIgnoreCase) ? AppLanguage.Turkish : AppLanguage.English;

    public static string Code(AppLanguage language) => language == AppLanguage.Turkish ? "tr" : "en";

    public static void Set(AppLanguage language)
    {
        Current = language;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language == AppLanguage.Turkish ? "tr-TR" : "en-US");
    }

    /// <summary>Text in the current language. Extra arguments fill {0}, {1}... in the translated text.</summary>
    public static string T(string english, params object?[] args)
    {
        string text = Current == AppLanguage.Turkish && TurkishByEnglish.TryGetValue(english, out var tr) ? tr : english;
        return args.Length == 0 ? text : string.Format(text, args);
    }

    /// <summary>
    /// Text that may already be in either language, put into the current one: a known text, or a
    /// filled-in template of the table. Unknown text comes back as it is.
    /// </summary>
    public static string Translate(string text)
    {
        if (text.Length == 0)
            return text;

        string english = EnglishByTurkish.TryGetValue(text, out var en) ? en : text;
        if (TurkishByEnglish.TryGetValue(english, out var tr))
            return Current == AppLanguage.Turkish ? tr : english;

        foreach (var (englishPattern, turkishPattern, englishKey, turkishText) in Templates)
        {
            var match = englishPattern.Match(text);
            bool isTurkish = false;
            if (!match.Success)
            {
                match = turkishPattern.Match(text);
                isTurkish = match.Success;
            }

            if (!match.Success)
                continue;

            // The filled-in values may be known texts themselves ("part 2 of 5: ..."): those follow too.
            var args = Arguments(match).Select(a => (object)Translate((string)a)).ToArray();
            if (Current == AppLanguage.Turkish)
                return isTurkish ? text : string.Format(turkishText, args);
            return isTurkish ? string.Format(englishKey, args) : text;
        }

        return text;
    }

    /// <summary>The English of a known text in either language ("geliyor" → "coming"); unknown text as it is.</summary>
    public static string English(string text) => EnglishByTurkish.TryGetValue(text, out var en) ? en : text;

    /// <summary>Re-texts a window or control and everything inside it, including list and grid column headers.</summary>
    public static void Apply(Control root)
    {
        // What the user types, and values in lists, are data, not interface text.
        if (root is not (TextBoxBase or ComboBox or NumericUpDown))
        {
            string translated = Translate(root.Text);
            if (translated != root.Text)
                root.Text = translated;
        }

        switch (root)
        {
            case ListView list:
                foreach (ColumnHeader column in list.Columns)
                    column.Text = Translate(column.Text);
                break;
            case DataGridView grid:
                foreach (DataGridViewColumn column in grid.Columns)
                    column.HeaderText = Translate(column.HeaderText);
                break;
        }

        foreach (Control child in root.Controls)
            Apply(child);
    }

    private static object[] Arguments(Match match)
    {
        var args = new List<object>();
        for (int i = 0; match.Groups[$"p{i}"].Success; i++)
            args.Add(match.Groups[$"p{i}"].Value);
        return args.ToArray();
    }

    /// <summary>The table's texts with {0}, {1}... as patterns in both languages, longest first.</summary>
    private static List<(Regex, Regex, string, string)> BuildTemplates()
    {
        var templates = new List<(Regex, Regex, string, string)>();
        foreach (var (english, turkish) in TurkishByEnglish.OrderByDescending(p => p.Key.Length))
        {
            if (!english.Contains('{') || Pattern(english) is not { } en || Pattern(turkish) is not { } tr)
                continue;
            templates.Add((en, tr, english, turkish));
        }

        return templates;
    }

    /// <summary>A regex for a template: each {n} (once) becomes a group "pn". Null for one that cannot be matched safely.</summary>
    private static Regex? Pattern(string template)
    {
        var seen = new HashSet<string>();
        bool ok = true;
        string pattern = Placeholder.Replace(Regex.Escape(template), m =>
        {
            string index = m.Groups[1].Value;
            if (!seen.Add(index))
                ok = false;
            return $"(?<p{index}>.+?)";
        });

        // Only placeholders and punctuation would match almost anything.
        if (!ok || seen.Count == 0 || Regex.Replace(template, @"\{[^}]*\}|[\W_]", "").Length < 2)
            return null;
        return new Regex("^" + pattern + "$", RegexOptions.Compiled | RegexOptions.Singleline);
    }

    private static Dictionary<string, string> Build() => new()
    {
        // ---- Shared: labels, directions, parts ----
        ["coming"] = "geliyor",
        ["going"] = "gidiyor",
        ["stopped"] = "duruyor",
        ["unclear"] = "belirsiz",
        ["unknown"] = "bilinmiyor",
        ["{0}? ({1})"] = "{0}? ({1})",
        ["(no plate)"] = "(plakasız)",
        ["no vehicles"] = "araç yok",
        ["not labelled"] = "etiketlenmedi",
        ["{0} playing part"] = "{0} oynayan bölüm",
        ["{0} playing parts"] = "{0} oynayan bölüm",
        [", {0} labelled"] = ", {0} etiketli",
        [" ({0} vehicle: {1} coming, {2} going)"] = " ({0} araç: {1} geliyor, {2} gidiyor)",
        [" ({0} vehicles: {1} coming, {2} going)"] = " ({0} araç: {1} geliyor, {2} gidiyor)",
        [", {0} to label"] = ", {0} etiketlenecek",
        ["; {0} label now in skipped parts"] = "; {0} etiket artık boşluklarda",
        ["; {0} labels now in skipped parts"] = "; {0} etiket artık boşluklarda",

        // ---- Shared: timeline ----
        ["Select a camera to see its timeline."] = "Zaman çizelgesini görmek için bir kamera seçin.",
        ["skip from {0}"] = "{0} itibarıyla boşluk",
        ["none"] = "yok",
        ["{0} coming, {1} going"] = "{0} geliyor, {1} gidiyor",
        ["skip {0} s"] = "boşluk {0} sn",
        ["Scanning motion {0}%"] = "Hareket taranıyor %{0}",
        ["Making previews {0}%"] = "Önizlemeler hazırlanıyor %{0}",
        ["{0} part, {1} labelled"] = "{0} bölüm, {1} etiketli",
        ["{0} parts, {1} labelled"] = "{0} bölüm, {1} etiketli",
        [", {0} vehicle"] = ", {0} araç",
        [", {0} vehicles"] = ", {0} araç",
        ["  (skipped)"] = "  (boşluk)",
        ["  plate {0}"] = "  plaka {0}",
        ["  part {0}: {1}"] = "  bölüm {0}: {1}",
        ["No preview"] = "Önizleme yok",
        ["No preview: {0}"] = "Önizleme yok: {0}",
        ["Label part {0} ({1})..."] = "Bölüm {0} etiketle ({1})...",
        ["Delete skip {0} - {1}"] = "{0} - {1} boşluğunu sil",
        ["Add a 5 s skip at {0}"] = "{0} konumuna 5 sn boşluk ekle",
        ["Clear all skips"] = "Tüm boşlukları temizle",

        // ---- Shared: where plates are read ----
        ["a detection area of {0} % x {1} % of the picture"] = "görüntünün %{0} x %{1} kadarlık bir tespit alanı",
        ["the whole picture"] = "tüm görüntü",
        ["{0}, with no road perspective"] = "{0}, yol perspektifi olmadan",
        ["{0}, with the road perspective"] = "{0}, yol perspektifiyle",
        ["no detection area"] = "tespit alanı yok",
        ["detection area"] = "tespit alanı",
        ["no road perspective"] = "yol perspektifi yok",
        ["road perspective"] = "yol perspektifi",

        // ---- Scanner window ----
        ["Video folder:"] = "Video klasörü:",
        ["Browse..."] = "Gözat...",
        ["Refresh"] = "Yenile",
        ["Language:"] = "Dil:",
        ["Video"] = "Video",
        ["Length"] = "Süre",
        ["Gaps"] = "Boşluklar",
        ["Plates read"] = "Okunan plakalar",
        ["Labelled"] = "Etiketli",
        ["Scan"] = "Tarama",
        ["Scan selected"] = "Seçilenleri tara",
        ["Scan all not scanned"] = "Taranmamışların hepsini tara",
        ["Stop"] = "Durdur",
        ["Take out of the queue"] = "Kuyruktan çıkar",
        ["Find gaps again"] = "Boşlukları yeniden bul",
        ["Read plates again"] = "Plakaları yeniden oku",
        ["New videos: movement (%)"] = "Yeni videolar: hareket (%)",
        ["at least (s)"] = "en az (sn)",
        ["keep (s)"] = "koru (sn)",
        ["A scan is running. Stop it and close? What was found so far is kept."] = "Bir tarama sürüyor. Durdurup kapatılsın mı? Şimdiye kadar bulunanlar korunur.",
        ["Folder with the videos to scan"] = "Taranacak videoların klasörü",
        ["No video files in this folder."] = "Bu klasörde video dosyası yok.",
        ["Checking {0} file(s)..."] = "{0} dosya denetleniyor...",
        ["{0} playable video(s)."] = "{0} oynatılabilir video.",
        ["file damaged"] = "dosya bozuk",
        ["not scanned"] = "taranmadı",
        ["{0}/{1}, {2} to check"] = "{0}/{1}, {2} denetlenecek",
        ["{0} car(s)"] = "{0} araç",
        ["The folder '{0}' does not exist."] = "'{0}' klasörü yok.",
        ["ffmpeg.exe not found. Put it in the '{0}' folder next to MockRtspScanner.exe."] = "ffmpeg.exe bulunamadı. MockRtspScanner.exe'nin yanındaki '{0}' klasörüne koyun.",
        ["Open folder"] = "Klasörü aç",
        ["Browse"] = "Gözat",
        ["Open video"] = "Videoyu aç",
        ["Scan all"] = "Hepsini tara",

        // ---- Scan queue ----
        ["waiting"] = "bekliyor",
        ["not scanned (stopped)"] = "taranmadı (durduruldu)",
        ["stopped; what was found so far is kept"] = "durduruldu; şimdiye kadar bulunanlar korundu",
        ["failed: {0}"] = "başarısız: {0}",
        ["finding gaps"] = "boşluklar bulunuyor",
        ["finding gaps {0}%"] = "boşluklar bulunuyor %{0}",
        ["reading plates"] = "plakalar okunuyor",
        ["reading plates {0}%: {1}"] = "plakalar okunuyor %{0}: {1}",
        ["part {0} of {1}: {2} car(s) so far"] = "bölüm {0}/{1}: şimdiye kadar {2} araç",
        ["part {0} of {1} done: {2} car(s) so far"] = "bölüm {0}/{1} bitti: şimdiye kadar {2} araç",
        ["done: {0} car(s) read"] = "bitti: {0} araç okundu",
        ["done: gaps found"] = "bitti: boşluklar bulundu",
        ["the motion scan failed: {0}"] = "hareket taraması başarısız: {0}",
        ["{0}: its file cannot be read ({1}). Fix or delete the file, then open the folder again."] = "{0}: dosyası okunamıyor ({1}). Dosyayı düzeltin veya silin, sonra klasörü yeniden açın.",

        // ---- Video editor ----
        ["Select a video on the left."] = "Soldan bir video seçin.",
        ["Select a video in the list on the left. Scan finds its gaps, plates and directions."] = "Soldaki listeden bir video seçin. Tarama boşluklarını, plakalarını ve yönlerini bulur.",
        ["{0}  -  its file cannot be read: {1}"] = "{0}  -  dosyası okunamıyor: {1}",
        ["{0}  -  {1}x{2}, {3}"] = "{0}  -  {1}x{2}, {3}",
        ["{0}  -  {1}x{2}, {3}  -  {4}"] = "{0}  -  {1}x{2}, {3}  -  {4}",
        ["Gaps: movement needed (% of the picture)"] = "Boşluklar: gereken hareket (görüntünün %'si)",
        ["gaps of at least (s)"] = "en az şu uzunlukta boşluklar (sn)",
        ["kept around movement (s)"] = "hareket çevresinde tutulan (sn)",
        ["Pick gaps again"] = "Boşlukları yeniden seç",
        ["Not scanned yet: Scan finds the gaps (still parts are cut when streaming)."] = "Henüz taranmadı: Tarama boşlukları bulur (yayında durağan bölümler kesilir).",
        ["Measuring motion..."] = "Hareket ölçülüyor...",
        ["{0} gap(s) picked with these settings."] = "Bu ayarlarla {0} boşluk seçildi.",
        ["{0} gap(s), changed by hand."] = "{0} boşluk, elle değiştirildi.",
        ["The gaps of {0} were changed by hand. Pick them again from the motion data with these settings? Your changes are replaced."] = "{0} videosunun boşlukları elle değiştirildi. Bu ayarlarla hareket verisinden yeniden seçilsin mi? Değişiklikleriniz silinir.",
        ["Playing parts"] = "Oynayan bölümler",
        ["#"] = "#",
        ["From"] = "Başlangıç",
        ["To"] = "Bitiş",
        ["Vehicles"] = "Araçlar",
        ["Detection area (R)"] = "Tespit alanı (R)",
        ["Road perspective (P)"] = "Yol perspektifi (P)",
        ["Whole picture"] = "Tüm görüntü",
        ["No perspective"] = "Perspektif yok",
        ["From Birel Plaka..."] = "Birel Plaka'dan...",
        ["Plates are read in {0}."] = "Plakalar şurada okunur: {0}.",
        ["Plates are read in {0}. The plates shown were read with another area: scan again (Read plates again) to use this one."] = "Plakalar şurada okunur: {0}. Gösterilen plakalar başka bir alanla okundu: bunu kullanmak için yeniden tarayın (Plakaları yeniden oku).",
        ["Plates are read in the whole picture, with no road perspective."] = "Plakalar tüm görüntüde, yol perspektifi olmadan okunur.",
        ["Birel Plaka's camera settings"] = "Birel Plaka'nın kamera ayarları",
        ["Birel Plaka camera settings ({0})|{0}|INI files (*.ini)|*.ini"] = "Birel Plaka kamera ayarları ({0})|{0}|INI dosyaları (*.ini)|*.ini",
        ["Cameras in {0}"] = "{0} içindeki kameralar",
        ["Another camera-settings.ini..."] = "Başka bir camera-settings.ini...",
        ["The area of {0} cannot be used:\n\n{1}"] = "{0} kamerasının alanı kullanılamıyor:\n\n{1}",
        ["Took the area of {0} ({1}). Birel Plaka keeps them as fractions of the picture, so they fit any size of the same view."] = "{0} ({1}) kamerasının alanı alındı. Birel Plaka bunları görüntünün oranları olarak tutar; aynı görüntünün her boyutuna uyar.",
        ["{0} is in a gap: it is not streamed, so it has no labels."] = "{0} bir boşlukta: yayınlanmaz, bu yüzden etiketi yok.",
        ["{0} s"] = "{0} sn",
        ["to check: {0}"] = "denetlenecek: {0}",
        ["{0}   [{1} read]"] = "{0}   [{1} okundu]",
        ["Not scanned for gaps yet: the whole video is one playing part. {0}"] = "Henüz boşluklar için taranmadı: tüm video tek bir oynayan bölüm. {0}",
        ["{0}; {1} prefilled from the plates, to check."] = "{0}; {1} plakalardan önceden dolduruldu, denetlenecek.",
        ["This video has nothing that plays."] = "Bu videoda oynayan bir şey yok.",
        ["Part {0} of {1}: {2} to {3} ({4} s)"] = "Bölüm {0}/{1}: {2} - {3} ({4} sn)",
        ["Part {0} of {1}: {2} to {3} ({4} s)  -  prefilled from the plates read: check it"] = "Bölüm {0}/{1}: {2} - {3} ({4} sn)  -  okunan plakalardan önceden dolduruldu: denetleyin",
        ["Part"] = "Bölüm",
        ["Time"] = "Zaman",
        ["Direction"] = "Yön",
        ["Plate (optional)"] = "Plaka (isteğe bağlı)",
        ["Add vehicle at the player's time"] = "Oynatıcının anında araç ekle",
        ["Remove"] = "Kaldır",
        ["No vehicles in this part"] = "Bu bölümde araç yok",
        ["Row {0}: '{1}' is not a time. Use seconds (83.4) or minutes and seconds (1:23.4)."] = "Satır {0}: '{1}' bir zaman değil. Saniye (83.4) veya dakika ve saniye (1:23.4) kullanın.",
        ["Row {0}: {1} is not in part {2} ({3} to {4})."] = "Satır {0}: {1}, {2}. bölümde değil ({3} - {4}).",
        ["Row {0}: choose coming or going."] = "Satır {0}: geliyor veya gidiyor seçin.",
        ["{0} (not saved until it is fixed)"] = "{0} (düzeltilene kadar kaydedilmez)",
        ["Plates read in this part"] = "Bu bölümde okunan plakalar",
        ["No plates read yet: Scan (on the left) reads them in every playing part."] = "Henüz plaka okunmadı: Tarama (solda) her oynayan bölümde okur.",
        ["No plates were read in this part."] = "Bu bölümde plaka okunmadı.",
        ["No plates were read in this part (not read yet)."] = "Bu bölümde plaka okunmadı (henüz okunmadı).",
        ["{0} car(s) read in this part. Check the reading in the player, then add it as a vehicle."] = "Bu bölümde {0} araç okundu. Okumayı oynatıcıda denetleyin, sonra araç olarak ekleyin.",
        ["Plate"] = "Plaka",
        ["Reads"] = "Okumalar",
        ["Result"] = "Sonuç",
        ["Readings of the selected car"] = "Seçili aracın okumaları",
        ["Best OCR"] = "En iyi OCR",
        ["added; {0}"] = "eklendi; {0}",
        ["decided ({0} reads agree)"] = "karar verildi ({0} okuma uyuşuyor)",
        ["not decided"] = "karar verilmedi",
        ["Add as vehicle"] = "Araç olarak ekle",
        ["Put in the selected vehicle"] = "Seçili araca koy",
        ["Add every car of this part"] = "Bu bölümün tüm araçlarını ekle",
        ["Select a vehicle in the table above first."] = "Önce yukarıdaki tablodan bir araç seçin.",
        ["Nothing tells the direction of a vehicle: choose coming or going in the table (it is saved then)."] = "Bir aracın yönü anlaşılamadı: tabloda geliyor veya gidiyor seçin (o zaman kaydedilir).",
        ["< Previous to label"] = "< Önceki etiketlenecek",
        ["Next to label >"] = "Sonraki etiketlenecek >",
        ["Delete labels in gaps"] = "Boşluklardaki etiketleri sil",
        ["Export CSV..."] = "CSV dışa aktar...",
        ["Ground-truth plates..."] = "Gerçek plakalar...",
        ["Prefill unlabelled parts"] = "Etiketsiz bölümleri önceden doldur",
        ["Every other playing part is labelled and checked."] = "Diğer tüm oynayan bölümler etiketli ve denetlendi.",
        ["Every playing part is labelled."] = "Tüm oynayan bölümler etiketli.",
        ["{0} label(s) are in gaps now, so they are never streamed. Delete them?"] = "{0} etiket artık boşluklarda, bu yüzden hiç yayınlanmaz. Silinsin mi?",
        ["Export labels of {0}"] = "{0} etiketlerini dışa aktar",
        ["CSV files (*.csv)|*.csv"] = "CSV dosyaları (*.csv)|*.csv",
        ["Export labels"] = "Etiketleri dışa aktar",
        ["No plates read yet."] = "Henüz plaka okunmadı.",
        ["No part without labels could be prefilled."] = "Etiketsiz hiçbir bölüm önceden doldurulamadı.",
        ["Prefilled {0} part(s) with {1} vehicle(s) (most likely plate and direction): check them with Next to label."] = "{0} bölüm {1} araçla önceden dolduruldu (en olası plaka ve yön): Sonraki etiketlenecek ile denetleyin.",
        ["{0} part(s) left to label: nothing tells a car's direction there."] = "{0} bölüm etiketlenmeye kaldı: oradaki bir aracın yönü anlaşılamadı.",
        ["part {0}: nothing tells a car's direction, left to label"] = "bölüm {0}: bir aracın yönü anlaşılamadı, etiketlenmeye kaldı",
        ["part {0} prefilled with {1} vehicle(s)"] = "bölüm {0}, {1} araçla önceden dolduruldu",

        // ---- Part player ----
        ["Play"] = "Oynat",
        ["Pause"] = "Duraklat",
        ["< Frame"] = "< Kare",
        ["Frame >"] = "Kare >",
        ["|< Start"] = "|< Başa",
        ["Loop"] = "Döngü",
        ["{0}   (part {1} to {2})"] = "{0}   (bölüm {1} - {2})",
        ["The video cannot be shown here: ffmpeg.exe was not found (see Requirements in the README)."] = "Video burada gösterilemiyor: ffmpeg.exe bulunamadı (README'deki Gereksinimler'e bakın).",
        ["The video cannot be shown: {0}"] = "Video gösterilemiyor: {0}",
        ["FFmpeg could not decode this part: {0}"] = "FFmpeg bu bölümü çözemedi: {0}",
        ["Select a playing part to see it here."] = "Burada görmek için bir oynayan bölüm seçin.",
        ["Loading the video..."] = "Video yükleniyor...",
        ["zoom {0}x  (double-click: whole picture)"] = "yakınlaştırma {0}x  (çift tık: tüm görüntü)",
        ["Detection area: drag over the picture to draw it (now the whole picture is read). Esc: undo. R: done."] = "Tespit alanı: çizmek için görüntü üzerinde sürükleyin (şu an tüm görüntü okunuyor). Esc: geri al. R: bitti.",
        ["Detection area: drag its edges or corners, drag inside to move it, drag outside it to draw a new one. Right-click: whole picture. Esc: undo. R: done."] = "Tespit alanı: kenarlarını veya köşelerini sürükleyin, taşımak için içinden, yenisini çizmek için dışından sürükleyin. Sağ tık: tüm görüntü. Esc: geri al. R: bitti.",
        ["Road perspective: drag the 4 corners onto the lane edges (a rectangle lying on the road). Drag inside to move it. Right-click: reset. Esc: undo. P: done."] = "Yol perspektifi: 4 köşeyi şerit kenarlarına sürükleyin (yolun üzerinde yatan bir dikdörtgen). Taşımak için içinden sürükleyin. Sağ tık: sıfırla. Esc: geri al. P: bitti.",
        ["far"] = "uzak",
        ["near"] = "yakın",

        // ---- Ground-truth plates dialog ----
        ["Plates in {0}"] = "{0} içindeki plakalar",
        ["The licence plates visible in this video, and when: the ground truth your ALPR system should read. Time is seconds (83.4) or minutes and seconds (1:23.4). The region is optional: where the plate is, as fractions of the picture from 0 to 1. Each time a plate's frame is streamed it is written to the ground-truth log. Plates in skipped parts are never streamed."] =
            "Bu videoda görünen plakalar ve ne zaman göründükleri: ALPR sisteminizin okuması gereken gerçek değerler. Zaman saniye (83.4) veya dakika ve saniyedir (1:23.4). Bölge isteğe bağlıdır: plakanın yeri, görüntünün 0 ile 1 arasındaki oranları olarak. Bir plakanın karesi her yayınlandığında gerçek değer günlüğüne yazılır. Boşluklardaki plakalar hiç yayınlanmaz.",
        ["Add at playhead ({0})"] = "Oynatma konumuna ekle ({0})",
        ["Add at playhead"] = "Oynatma konumuna ekle",
        ["Plates"] = "Plakalar",
        ["Import CSV..."] = "CSV içe aktar...",
        ["OK"] = "Tamam",
        ["Cancel"] = "İptal",
        ["x"] = "x",
        ["y"] = "y",
        ["width"] = "genişlik",
        ["height"] = "yükseklik",
        ["Row {0}: '{1}' is not a number."] = "Satır {0}: '{1}' bir sayı değil.",
        ["Row {0}: {1}."] = "Satır {0}: {1}.",
        ["Import plates for {0}"] = "{0} için plakaları içe aktar",
        ["Export plates of {0}"] = "{0} plakalarını dışa aktar",
        ["CSV files (*.csv)|*.csv|All files (*.*)|*.*"] = "CSV dosyaları (*.csv)|*.csv|Tüm dosyalar (*.*)|*.*",
        ["Import plates"] = "Plakaları içe aktar",
        ["Export plates"] = "Plakaları dışa aktar",

        // ---- Why a plate was not decided (saved in English with each reading; Birel Plaka's words) ----
        ["Too few reads ({0}, at least {1} needed)"] = "Çok az okuma ({0}, en az {1} gerekli)",
        ["No characters could be read"] = "Hiçbir karakter okunamadı",
        ["Plate may be cut off: most reads touch the edge of the detection area"] = "Plaka kesilmiş olabilir: okumaların çoğu tespit alanının kenarına değiyor",
        ["Plate may be cut off: most reads touch the edge of the picture"] = "Plaka kesilmiş olabilir: okumaların çoğu görüntünün kenarına değiyor",
        ["Character {0} was seen by too few reads"] = "{0}. karakteri çok az okuma gördü",
        ["Reads disagree on character {0}"] = "Okumalar {0}. karakterde uyuşmuyor",
        ["Several reads back a different answer for character {0}"] = "Birkaç okuma {0}. karakter için farklı bir cevabı destekliyor",
        ["Several reads did not fit this plate (another car on the same spot?)"] = "Birkaç okuma bu plakaya uymadı (aynı yerde başka bir araç mı?)",
    };
}
