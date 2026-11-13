using System.Globalization;

namespace MockRtsp;

public enum AppLanguage { English, Turkish }

/// <summary>
/// English and Turkish text for the whole interface. The English text is the key, so code and
/// designers keep writing plain English; <see cref="Apply"/> walks a form and swaps every known
/// text for the chosen language, and <see cref="T(string, object[])"/> translates text built at run time.
/// Text that is not in the table (file names, user input, log lines) is left alone.
/// The choice is kept in %APPDATA%\MockRtsp\language.txt; the first run follows the Windows language.
/// </summary>
public static class Loc
{
    private static readonly Dictionary<string, string> TurkishByEnglish = Build();
    private static readonly Dictionary<string, string> EnglishByTurkish =
        TurkishByEnglish.GroupBy(p => p.Value).ToDictionary(g => g.Key, g => g.First().Key);

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MockRtsp", "language.txt");

    public static AppLanguage Current { get; private set; } = Load();

    /// <summary>Native names for the language combobox, in <see cref="AppLanguage"/> order.</summary>
    public static readonly string[] Names = { "English", "Türkçe" };

    public static void Set(AppLanguage language)
    {
        Current = language;
        CultureInfo.CurrentUICulture = Culture;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, language == AppLanguage.Turkish ? "tr" : "en");
        }
        catch (Exception ex)
        {
            ErrorReporter.Report(ex, "Saving the language");
        }
    }

    private static CultureInfo Culture =>
        CultureInfo.GetCultureInfo(Current == AppLanguage.Turkish ? "tr-TR" : "en-US");

    private static AppLanguage Load()
    {
        AppLanguage language;
        try
        {
            language = File.Exists(FilePath)
                ? File.ReadAllText(FilePath).Trim().Equals("tr", StringComparison.OrdinalIgnoreCase)
                    ? AppLanguage.Turkish : AppLanguage.English
                : CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tr" ? AppLanguage.Turkish : AppLanguage.English;
        }
        catch
        {
            language = AppLanguage.English;
        }

        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language == AppLanguage.Turkish ? "tr-TR" : "en-US");
        return language;
    }

    /// <summary>Text in the current language. Extra arguments fill {0}, {1}... in the translated text.</summary>
    public static string T(string english, params object[] args)
    {
        string text = Translate(english);
        return args.Length == 0 ? text : string.Format(text, args);
    }

    /// <summary>Translates text that may already be in either language; unknown text comes back as it is.</summary>
    public static string Translate(string text)
    {
        if (text.Length == 0)
            return text;

        string english = EnglishByTurkish.TryGetValue(text, out var en) ? en : text;
        return Current == AppLanguage.Turkish && TurkishByEnglish.TryGetValue(english, out var tr) ? tr : english;
    }

    /// <summary>Re-texts a form or control and everything inside it, including headers, tabs, menus and tooltips.</summary>
    public static void Apply(Control root)
    {
        Set(root);
        foreach (Control child in root.Controls)
            Apply(child);

        switch (root)
        {
            case ListView list:
                foreach (ColumnHeader column in list.Columns)
                    column.Text = Translate(column.Text);
                break;
            case StatusStrip strip:
                foreach (ToolStripItem item in strip.Items)
                    item.Text = Translate(item.Text);
                break;
        }
    }

    private static void Set(Control control)
    {
        // What the user types or the app logs is data, not interface text.
        if (control is TextBoxBase or ComboBox or NumericUpDown)
            return;

        string translated = Translate(control.Text);
        if (translated != control.Text)
            control.Text = translated;
    }

    /// <summary>Translates the tooltips of a ToolTip component for the given controls.</summary>
    public static void ApplyTips(ToolTip tips, IEnumerable<Control> controls)
    {
        foreach (var control in controls)
        {
            string? tip = tips.GetToolTip(control);
            if (!string.IsNullOrEmpty(tip))
                tips.SetToolTip(control, Translate(tip));
        }
    }

    private static Dictionary<string, string> Build() => new()
    {
        // ---- Main window ----
        ["MockRtsp - mock RTSP cameras"] = "MockRtsp - sahte RTSP kameraları",
        ["MockRtsp - {0}"] = "MockRtsp - {0}",
        ["Port:"] = "Port:",
        ["Start server"] = "Sunucuyu başlat",
        ["Stop server"] = "Sunucuyu durdur",
        ["Settings (defaults and server)..."] = "Ayarlar (varsayılanlar ve sunucu)...",
        ["Open project..."] = "Proje aç...",
        ["Save project"] = "Projeyi kaydet",
        ["Save project as..."] = "Projeyi farklı kaydet...",
        ["Reopen the last project at startup"] = "Açılışta son projeyi yeniden aç",
        ["Language:"] = "Dil:",
        ["Video folder:"] = "Video klasörü:",
        ["Browse..."] = "Gözat...",
        ["Refresh"] = "Yenile",
        ["Choose the folder with your camera videos."] = "Kamera videolarınızın bulunduğu klasörü seçin.",
        ["Choose the folder with your camera videos"] = "Kamera videolarınızın bulunduğu klasörü seçin",
        ["Videos (double-click to add as a camera; select several for a playlist)"] = "Videolar (kamera olarak eklemek için çift tıklayın; oynatma listesi için birkaçını seçin)",
        ["Video"] = "Video",
        ["Length"] = "Süre",
        ["Picture"] = "Görüntü",
        ["Skips"] = "Atlamalar",
        ["Camera"] = "Kamera",
        ["Stream path:"] = "Yayın yolu:",
        ["Add camera"] = "Kamera ekle",
        ["Cameras"] = "Kameralar",
        ["Start the server, then double-click a video on the left to add it as a camera."] = "Sunucuyu başlatın, sonra soldaki bir videoya çift tıklayarak kamera olarak ekleyin.",
        ["Server stopped"] = "Sunucu durduruldu",
        ["MediaMTX not found"] = "MediaMTX bulunamadı",
        ["MediaMTX running on port {0}  |  cameras: {1}"] = "MediaMTX {0} portunda çalışıyor  |  kameralar: {1}",
        ["No video files in this folder."] = "Bu klasörde video dosyası yok.",
        ["Checking {0} file(s)..."] = "{0} dosya denetleniyor...",
        ["{0} playable video(s)"] = "{0} oynatılabilir video",
        [", {0} other file(s) hidden"] = ", {0} diğer dosya gizlendi",
        ["Open a project"] = "Proje aç",
        ["Save the setup as a project"] = "Kurulumu proje olarak kaydet",
        ["Save these cameras and settings as a project, so they can be opened again next time?"] = "Bu kameralar ve ayarlar, bir dahaki sefere yeniden açılabilmesi için proje olarak kaydedilsin mi?",
        ["Save the changes to {0}?"] = "{0} dosyasındaki değişiklikler kaydedilsin mi?",
        ["Replace the {0} skip(s) of {1} with the result of a new motion scan?"] = "{1} videosunun {0} atlaması yeni bir hareket taramasının sonucuyla değiştirilsin mi?",
        ["{0} has skips you changed by hand. Pick its skips again from the saved motion data, with the new motion settings? Your changes would be replaced."] = "{0} videosunda elle değiştirdiğiniz atlamalar var. Atlamalar, yeni hareket ayarlarıyla kayıtlı hareket verisinden yeniden seçilsin mi? Değişiklikleriniz silinir.",
        ["New motion settings"] = "Yeni hareket ayarları",
        ["{0} is being scanned already."] = "{0} zaten taranıyor.",
        ["Start the camera first, then open it in VLC."] = "Önce kamerayı başlatın, sonra VLC'de açın.",
        ["Settings"] = "Ayarlar",
        ["Video folder"] = "Video klasörü",
        ["Open project"] = "Proje aç",
        ["Check the settings"] = "Ayarları denetleyin",
        ["Add videos to the playlist"] = "Oynatma listesine video ekle",
        ["Add at least one video."] = "En az bir video ekleyin.",
        ["Videos ({0})|{0}|All files (*.*)|*.*"] = "Videolar ({0})|{0}|Tüm dosyalar (*.*)|*.*",
        ["MockRtsp projects (*{0})|*{0}|All files (*.*)|*.*"] = "MockRtsp projeleri (*{0})|*{0}|Tüm dosyalar (*.*)|*.*",

        // ---- Camera panel ----
        ["OFF"] = "KAPALI",
        ["LIVE"] = "CANLI",
        ["PAUSED"] = "DURAKLATILDI",
        ["Start"] = "Başlat",
        ["Stop"] = "Durdur",
        ["Open in VLC"] = "VLC'de aç",
        ["Playlist..."] = "Oynatma listesi...",
        ["Video settings..."] = "Video ayarları...",
        ["Scan motion"] = "Hareketi tara",
        ["Scanning {0}%"] = "Taranıyor %{0}",
        ["Outage (s):"] = "Kesinti (sn):",
        ["Simulate"] = "Simüle et",
        ["Remove"] = "Kaldır",
        ["Rewind"] = "Başa sar",
        ["« 10 s"] = "« 10 sn",
        ["10 s »"] = "10 sn »",
        ["Pause"] = "Duraklat",
        ["Play"] = "Oynat",
        ["Next"] = "Sonraki",
        ["Speed:"] = "Hız:",
        ["Skip start"] = "Atlama başı",
        ["Skip end"] = "Atlama sonu",
        ["Fit"] = "Sığdır",
        ["no skips"] = "atlama yok",
        ["{0} skip(s)"] = "{0} atlama",
        ["{0} videos"] = "{0} video",
        ["own settings"] = "özel ayarlar",
        ["default settings"] = "varsayılan ayarlar",

        // ---- Playlist dialog ----
        ["Playlist"] = "Oynatma listesi",
        ["Playlist for {0}"] = "{0} için oynatma listesi",
        ["Playlist: plays top to bottom"] = "Oynatma listesi: yukarıdan aşağıya oynar",
        ["Videos in the folder"] = "Klasördeki videolar",
        ["Add  >"] = "Ekle  >",
        ["<  Remove"] = "<  Kaldır",
        ["Move up"] = "Yukarı taşı",
        ["Move down"] = "Aşağı taşı",
        ["Other file..."] = "Başka dosya...",
        ["OK"] = "Tamam",
        ["Cancel"] = "İptal",
        ["After the last video the first starts again (unless looping is off in the settings). The camera's picture size, frame rate and quality come from the first video's settings; videos of another shape get black bars. Each video keeps its own skips."] =
            "Son videodan sonra ilki yeniden başlar (ayarlarda döngü kapalı değilse). Kameranın görüntü boyutu, kare hızı ve kalitesi ilk videonun ayarlarından gelir; farklı boyuttaki videolara siyah çubuklar eklenir. Her video kendi atlamalarını korur.",

        // ---- Settings checks ----
        ["Numeric values cannot be negative."] = "Sayısal değerler negatif olamaz.",
        ["Width and height must both be set, or both left at 0."] = "Genişlik ve yükseklik ya birlikte ayarlanmalı ya da ikisi de 0 bırakılmalı.",
        ["Width and height must be even numbers (H.264 requires it)."] = "Genişlik ve yükseklik çift sayı olmalı (H.264 bunu gerektirir).",
        ["Frame rate can be at most 120."] = "Kare hızı en fazla 120 olabilir.",
        ["Movement needed must be between 0.1 and 25 % of the picture."] = "Gereken hareket görüntünün %0,1 ile %25'i arasında olmalı.",
        ["Still parts must be at least 1 second, and the time kept around movement cannot be negative."] = "Durağan bölümler en az 1 saniye olmalı ve hareket çevresinde tutulan süre negatif olamaz.",
        ["Publish transport must be tcp or udp."] = "Yayın aktarımı tcp veya udp olmalı.",
        ["The publish transport must be one the server accepts (RTSP transports on the Server tab)."] = "Yayın aktarımı sunucunun kabul ettiği biri olmalı (Sunucu sekmesindeki RTSP aktarımları).",
        ["Timeouts must be at least 1 second."] = "Zaman aşımları en az 1 saniye olmalı.",
        ["Viewer login needs both a username and a password."] = "İzleyici girişi için hem kullanıcı adı hem parola gerekir.",
        ["Publisher login needs both a username and a password."] = "Yayıncı girişi için hem kullanıcı adı hem parola gerekir.",
        ["Logins can only use letters, digits and ! $ ( ) * + . ; < = > [ ] ^ _ - @ # (MediaMTX rejects anything else)."] = "Girişlerde yalnızca harf, rakam ve ! $ ( ) * + . ; < = > [ ] ^ _ - @ # kullanılabilir (MediaMTX başka her şeyi reddeder).",

        // ---- Settings dialog ----
        ["Settings for {0}"] = "{0} ayarları",
        ["Use defaults"] = "Varsayılanları kullan",
        ["Reset defaults"] = "Varsayılanlara dön",
        ["Defaults for videos"] = "Videolar için varsayılanlar",
        ["Server"] = "Sunucu",
        ["Playback"] = "Oynatma",
        ["Publishing"] = "Yayınlama",
        ["Logging"] = "Günlük",
        ["Skip still parts longer than (seconds):"] = "Şundan uzun durağan bölümleri atla (saniye):",
        ["Keep before and after movement (seconds):"] = "Hareketten önce ve sonra tut (saniye):",
        ["Movement needed (% of the picture):"] = "Gereken hareket (görüntünün %'si):",
        ["Motion scan (skip parts where nothing moves)"] = "Hareket taraması (hiçbir şeyin hareket etmediği bölümleri atla)",
        ["Scan a video for motion the first time it is used (videos with saved data are not rescanned)"] = "Bir videoyu ilk kullanıldığında hareket için tara (verisi kayıtlı videolar yeniden taranmaz)",
        ["Loop the video when it ends (otherwise hold the last frame)"] = "Video bitince başa sar (aksi halde son kare kalır)",
        ["Resolution and quality"] = "Çözünürlük ve kalite",
        ["Resolution (width x height, 0 = keep):"] = "Çözünürlük (genişlik x yükseklik, 0 = koru):",
        ["Frame rate (fps, 0 = keep):"] = "Kare hızı (fps, 0 = koru):",
        ["Bitrate (kbps, 0 = default):"] = "Bit hızı (kbps, 0 = varsayılan):",
        ["Keyframe interval (frames, 0 = every second):"] = "Anahtar kare aralığı (kare, 0 = her saniye):",
        ["Disable B-frames (lower latency)"] = "B-karelerini kapat (daha düşük gecikme)",
        ["Transport to MediaMTX:"] = "MediaMTX'e aktarım:",
        ["FFmpeg log level:"] = "FFmpeg günlük düzeyi:",
        ["MediaMTX log level:"] = "MediaMTX günlük düzeyi:",
        ["RTSP transports:"] = "RTSP aktarımları:",
        ["TCP and UDP"] = "TCP ve UDP",
        ["TCP only"] = "Yalnızca TCP",
        ["UDP only"] = "Yalnızca UDP",
        ["Network and timeouts"] = "Ağ ve zaman aşımları",
        ["Read timeout (seconds):"] = "Okuma zaman aşımı (saniye):",
        ["Write timeout (seconds):"] = "Yazma zaman aşımı (saniye):",
        ["Viewer login (optional, required to watch)"] = "İzleyici girişi (isteğe bağlı, izlemek için gerekir)",
        ["Publisher login (optional, required to send)"] = "Yayıncı girişi (isteğe bağlı, göndermek için gerekir)",
        ["Username (blank = none):"] = "Kullanıcı adı (boş = yok):",
        ["Password:"] = "Parola:",
        ["The RTSP server. Changes here apply after you stop and start the server."] = "RTSP sunucusu. Buradaki değişiklikler sunucuyu durdurup başlattıktan sonra uygulanır.",
        ["Used by every video that has no settings of its own (give a video its own with Video settings on its camera). Cameras restart with new settings, keeping position, pause and speed. New motion settings pick skips again from saved scans, without scanning again."] =
            "Kendi ayarı olmayan her video tarafından kullanılır (bir videoya kendi ayarını, kamerasındaki Video ayarları ile verin). Kameralar yeni ayarlarla yeniden başlar; konum, duraklatma ve hız korunur. Yeni hareket ayarları atlamaları kayıtlı taramalardan yeniden seçer, yeniden taramaz.",
        ["Settings for this video only, saved next to it in {0}.mockrtsp.json. Cameras playing it restart with the new settings, keeping position, pause and speed. New motion settings pick skips again from the saved scan, without scanning again."] =
            "Yalnızca bu video için ayarlar; yanında {0}.mockrtsp.json dosyasına kaydedilir. Videoyu oynatan kameralar yeni ayarlarla yeniden başlar; konum, duraklatma ve hız korunur. Yeni hareket ayarları atlamaları kayıtlı taramadan yeniden seçer, yeniden taramaz.",
        ["Replay the video from the start when it reaches the end. Off stops the camera after one pass."] = "Video sona erince baştan oynat. Kapalıysa kamera bir geçişten sonra durur.",
        ["Lower finds smaller movement (a person far away); higher ignores small changes such as leaves or rain."] = "Düşük değer daha küçük hareketleri (uzaktaki bir kişi) bulur; yüksek değer yaprak veya yağmur gibi küçük değişiklikleri yok sayar.",
        ["Still parts shorter than this keep playing, so short pauses in the action are not cut."] = "Bundan kısa durağan bölümler oynamaya devam eder, böylece olaydaki kısa duraklar kesilmez.",
        ["Time kept before movement starts and after it ends, so a car is seen arriving and leaving."] = "Hareket başlamadan önce ve bittikten sonra tutulan süre; böylece bir aracın gelişi ve gidişi görülür.",
        ["A new viewer shows nothing until the next keyframe. 0 sends one every second, so playback starts within about a second."] = "Yeni bir izleyici bir sonraki anahtar kareye kadar görüntü görmez. 0 her saniye bir tane gönderir; oynatma yaklaşık bir saniyede başlar.",
        ["Which transports MediaMTX accepts from viewers. TCP is the most reliable."] = "MediaMTX'in izleyicilerden kabul ettiği aktarımlar. TCP en güvenilir olanıdır.",
        ["Set both a username and a password to require a login for viewers."] = "İzleyiciler için giriş istemek üzere hem kullanıcı adı hem parola girin.",
        ["Set both a username and a password to require a login for the publisher."] = "Yayıncı için giriş istemek üzere hem kullanıcı adı hem parola girin.",
    };
}
