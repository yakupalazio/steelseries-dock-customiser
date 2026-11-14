using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Independent factual model-name index for wireless peripherals only.
/// Product names and public VID/PID facts are not AGPL implementation code.
/// No OpenMouse drivers, curated notes, registry logic, or API is embedded.
/// "Recognized" NEVER means a working battery reader.
/// </summary>
internal static class WirelessCompatibilityCatalog
{
    internal sealed record DeviceModel(
        string Brand,string Model,string Category,string ProtocolHint,
        string ReaderStatus);

    internal sealed record Match(
        string Brand,string? Model,string Category,
        string ReaderStatus,string Confidence)
    {
        internal bool ExactModel=>Model is not null;
        internal string Name=>Model is null?Brand+" wireless device":
            Brand+" "+Model;
    }

    // Publicly named wireless models, independently grouped by manufacturer.
    // Generic wired-only products, USB-cabled coolers, charging docks, etc.
    // deliberately excluded. "Model recognized" does not enable HID writes.
    private static readonly (string Brand,string Category,string Names)[]
        Groups=[
        ("ATK","Mouse",
            "A9 Plus;A9 Air;A9 Mini Plus;A9 Ultimate Plus;A9;"+
            "F1 V2 Ultra Max;F1 Ultimate;F1 Ultimate 2.0;"+
            "X1 V2 Ultimate;X1 Pro;X1;VXE Mad R Major;"+
            "VXE Mad R Major Plus;VXE Dragonfly R1 Pro;"+
            "ZERO;VXE Zero Normal;G3 Pro;Ghost"),
        ("WLMOUSE","Mouse",
            "Beast X Pro;Beast X Mini;Beast Mini;Beast Mini Pro;"+
            "Beast X Max;Beast Max;Beast X;Beast X V2;Beast X 4K;"+
            "Sword X;YING 63;YING 65;Ying;Huàn;Huan M;Mini;"+
            "Beast Miao;Beast G;Strider;W1"),
        ("Wraith","Mouse",
            "W1"),
        ("Scyrox","Mouse",
            "V8S;V8;V6"),
        ("Kysona","Mouse",
            "Mercury Pro;Aztec;M600 V2"),
        ("Yeah Magic","Mouse",
            "Tyrfig V1"),
        ("Hitscan","Mouse",
            "Hyperlight"),
        ("AJAZZ","Mouse",
            "AJ179 Pro;AJ139 Pro;AJ159;AJ159 APEX"),
        ("Logitech","Mouse",
            "G Pro X Superlight 2 DEX;G Pro X Superlight 2 SE;"+
            "G Pro X Superlight 2;G Pro X Superlight;"+
            "G Pro 2 Lightspeed;G Pro Wireless;"+
            "G502 X Plus;G502 X Lightspeed;G502 Lightspeed;"+
            "G305;G304;G309 Lightspeed;G303 Shroud Edition;"+
            "G604 Lightspeed;G603 Lightspeed;G703 Lightspeed;"+
            "G903 Lightspeed;G705;MX Master 4;MX Master 3S;"+
            "MX Master 3;MX Master 2S;MX Anywhere 3S;"+
            "MX Anywhere 3;MX Vertical;MX Ergo S;Lift Vertical;"+
            "M650;M575S;M510;Pebble M350s"),
        ("Logitech","Keyboard",
            "G915 Lightspeed;G915 TKL;G915 X Lightspeed;"+
            "G613;G715;PRO X TKL Lightspeed;"+
            "MX Keys;MX Keys S;MX Keys Mini;MX Mechanical;"+
            "MX Mechanical Mini;Craft;K380;Pebble Keys 2 K380s"),
        ("Razer","Mouse",
            "Viper V4 Pro;Viper V3 Pro;Viper V3 Pro SE;"+
            "Viper V3 HyperSpeed;Viper V2 Pro;Viper Ultimate;"+
            "Viper Mini Signature Edition;DeathAdder V4 Pro;"+
            "DeathAdder V3 Pro;DeathAdder V3 HyperSpeed;"+
            "DeathAdder V2 Pro;DeathAdder V2 X HyperSpeed;"+
            "Basilisk V3 Pro 35K;Basilisk V3 Pro;"+
            "Basilisk V3 X HyperSpeed;Basilisk X HyperSpeed;"+
            "Basilisk Ultimate;Naga V2 Pro;Naga V2 HyperSpeed;"+
            "Naga Pro;Cobra Pro;Orochi V2;Atheris"),
        ("Razer","Keyboard",
            "BlackWidow V4 Pro 75%;BlackWidow V3 Pro;"+
            "BlackWidow V3 Mini HyperSpeed;DeathStalker V2 Pro;"+
            "DeathStalker V2 Pro TKL;Pro Type Ultra"),
        ("Razer","Headset",
            "BlackShark V2 Pro;BlackShark V3 Pro;"+
            "Barracuda Pro;Barracuda X;Kraken V3 Pro;"+
            "Kraken V4 Pro;Kaira Pro;Kaira HyperSpeed"),
        ("SteelSeries","Mouse",
            "Aerox 3 Wireless;Aerox 5 Wireless;Aerox 9 Wireless;"+
            "Aerox 3 Gen 2;Rival 3 Wireless;Prime Wireless;"+
            "Prime Mini Wireless"),
        ("SteelSeries","Headset",
            "Arctis Nova Pro Wireless;Arctis Nova 7;"+
            "Arctis Nova 5 Wireless;Arctis Nova 3 Wireless;"+
            "Arctis 7 Wireless;Arctis 9 Wireless;Arctis Pro Wireless"),
        ("SteelSeries","Keyboard",
            "Apex Pro Mini Wireless;Apex Pro TKL Wireless"),
        ("Corsair","Mouse",
            "Dark Core RGB Pro SE;Dark Core RGB Pro;"+
            "M65 RGB Ultra Wireless;Katar Elite Wireless;"+
            "Ironclaw RGB Wireless;Harpoon RGB Wireless;"+
            "Scimitar Elite Wireless;Sabre RGB Pro Wireless;"+
            "Nightsabre Wireless;M75 Wireless"),
        ("Corsair","Keyboard",
            "K100 AIR Wireless;K70 Pro Mini Wireless;"+
            "K65 Plus Wireless;K65 Pro Mini;K83 Wireless"),
        ("Corsair","Headset",
            "Virtuoso RGB Wireless XT;Virtuoso Pro Wireless;"+
            "HS80 RGB Wireless;HS80 MAX;HS65 Wireless;"+
            "Void RGB Elite Wireless;HS55 Wireless"),
        ("ASUS","Mouse",
            "ROG Harpe Ace Aim Lab;ROG Harpe Ace Mini;"+
            "ROG Harpe II Ace;ROG Keris Wireless AimPoint;"+
            "ROG Gladius III Wireless AimPoint;"+
            "ROG Strix Impact III Wireless;"+
            "ROG Chakram X Origin;ROG Pugio II;"+
            "ROG Spatha X;TUF Gaming M4 Wireless"),
        ("ASUS","Keyboard",
            "ROG Azoth;ROG Azoth Extreme;ROG Falchion RX Low Profile;"+
            "ROG Falchion Ace HFX;ROG Claymore II"),
        ("ASUS","Headset",
            "ROG Delta II Wireless;ROG Pelta Wireless;"+
            "ROG Strix Go 2.4;ROG Cetra True Wireless"),
        ("HyperX","Mouse",
            "Pulsefire Haste Wireless;Pulsefire Haste 2 Wireless;"+
            "Pulsefire Haste 2 Pro;Pulsefire Dart;"+
            "Pulsefire Saga Pro"),
        ("HyperX","Keyboard",
            "Alloy Rise 75 Wireless;Alloy Origins 60 Wireless"),
        ("HyperX","Headset",
            "Cloud III Wireless;Cloud II Wireless;"+
            "Cloud Alpha Wireless;Cloud Flight;Cloud Flight S;"+
            "Cloud Stinger Core Wireless;Cloud MIX Buds"),
        ("Pulsar","Mouse",
            "X2 V3 ES Mini;X2H V3 ES;X2 Mini;X2 V2;"+
            "X2F;X2A V3;X2H;X3 Medium;X3 CrazyLight;"+
            "X2N CrazyLight;Tenz Signature;Xlite V3;Xlite V4"),
        ("Lamzu","Mouse",
            "Atlantis OG V2;Atlantis Mini;Atlantis Mini Pro;"+
            "Atlantis;Maya X;Maya;Maya Champion Edition;"+
            "Thorn V2;Thorn;Inca;Paro"),
        ("VAXEE","Mouse",
            "NP-01S V3 Wireless;NP-01S V2 Wireless;"+
            "NP-01S Ergo Wireless;NP-01S Wireless;"+
            "NP-01 Ergo Wireless;NP-01 Wireless;"+
            "XE V2 Wireless;XE-S-L Wireless;XE-S Wireless;"+
            "VAXEE x Ninjutso Sora;"+
            "XE Wireless;AX Wireless;E1 Wireless"),
        ("VGN","Mouse",
            "Dragonfly F2 Master Plus;F2 Pro Max;F1 MOBA;"+
            "Dragonfly R1 Pro;Y2 Ultra"),
        ("MCHOSE","Mouse",
            "A7 V3 Ultra Plus;A7 V3 Pro Plus;A7 V3 Pro;A7 V3;"+
            "A7 V2 Ultra Plus;A7 V2 Ultra;A7 V2 Pro Plus;"+
            "A7 V2 Pro;A5 Pro Max;L7 Ultra Plus;"+
            "AX5;A7X Ultra"),
        ("Keychron","Mouse",
            "M6 4K;M6 SE;M6;M3 Mini 4K;M3 Mini;"+
            "M3 V2;M3;M2 Mini 8K;M2 Mini;M2;"+
            "M4 4K;M4;M5 SE;M5;M7 8K;M8;"+
            "Nape Pro;G3 Air"),
        ("Keychron","Keyboard",
            "Q1 Max;Q3 Max;Q5 Max;Q6 Max;"+
            "K2 Pro;K3 Pro;K8 Pro;K10 Pro;"+
            "K2 Max;K3 Max;K8 Max;K10 Max;"+
            "V1 Max;V3 Max;V5 Max;V6 Max;Lemokey L3"),
        ("Glorious","Mouse",
            "Model O 2 Wireless;Model O 2 Pro;Model D 2 Pro;"+
            "Model I 2 Wireless;Model I 2 Bluetooth;"+
            "Model O 2 Mini Bluetooth;Series One Pro"),
        ("Finalmouse","Mouse",
            "UltralightX;UltralightX Pro;Starlight-12;"+
            "Starlight Pro;Zeus;Achilles"),
        ("Endgame Gear","Mouse",
            "XM2w 4K;XM2w;OP1we;OP1w 4K"),
        ("Ninjutso","Mouse",
            "Sora V2;Sora 4K;Origin One X;Katana Superlight"),
        ("Delux","Mouse",
            "M800 Mini;M800 Pro;M900 Pro;M700 Ultra;M900 Mini"),
        ("Dareu","Mouse",
            "A950 PRO Mg;EM901"),
        ("Rapoo","Mouse",
            "VT9 Pro;VT3;VT7 Max;MT760 Pro"),
        ("G-Wolves","Mouse",
            "HTX Ultra;HTX Mini;HTX Mini 8K;HTX;"+
            "HTS Plus;HTS Plus Pro;HTS Ultra;"+
            "HSK Pro;HSK Plus;HTR Pro;HTR;Fenrir"),
        ("Attack Shark","Mouse",
            "X3 Pro;X3;X6;X11;X8 Ultra;X8 Plus;"+
            "R11 Ultra;X12;X1;V6"),
        ("Pwnage","Mouse",
            "Ultra Custom Symm;Stormbreaker;Symm 3"),
        ("Roccat","Mouse",
            "Kone Pro Air;Burst Pro Air;Kone XP Air"),
        ("Turtle Beach","Headset",
            "Stealth 700 Gen 3;Stealth 600 Gen 3;"+
            "Stealth Pro;Atlas Air"),
        ("Sony","Controller",
            "DualSense Edge;DualSense;DualShock 4"),
        ("Microsoft","Controller",
            "Xbox Elite Wireless Controller Series 2;"+
            "Xbox Wireless Controller"),
        ("Nintendo","Controller",
            "Switch Pro Controller;Joy-Con")
    ];

    private static readonly DeviceModel[] Items=Groups.SelectMany(group=>
        group.Names.Split(';',StringSplitOptions.RemoveEmptyEntries|
            StringSplitOptions.TrimEntries)
        .Select(model=>new DeviceModel(
            group.Brand,model,group.Category,
            ProtocolHint(group.Brand),
            BatteryProviderState(group.Brand))))
        .DistinctBy(item=>Normalize(item.Brand+item.Model)+item.Category)
        .ToArray();

    private static string ProtocolHint(string brand)=>brand switch{
        "Logitech"=>"HID++ / BLE / standart HID",
        "Razer"=>"Bluetooth GATT / üretici HID",
        "SteelSeries"=>"GG Engine / Bluetooth / HID",
        "Corsair"=>"iCUE SDK / Bluetooth / HID",
        "Sony" or "Microsoft" or "Nintendo"=>"Bluetooth / HID",
        _=>"Bluetooth GATT / üreticiye özel HID"
    };

    private static string BatteryProviderState(string brand)=>brand switch{
        "SteelSeries"=>"GG veya standart protokol varsa okunabilir",
        "Corsair"=>"iCUE SDK veya standart protokol varsa okunabilir",
        _=>"Model tanıma; canlı pil protokolü test edilmeli"
    };

    internal static IReadOnlyList<DeviceModel> All=>Items;
    internal static int ModelCount=>Items.Length;
    internal static int BrandCount=>Items.Select(x=>x.Brand)
        .Distinct(StringComparer.OrdinalIgnoreCase).Count();

    internal static string Normalize(string text)=>
        new string((text??"").ToLowerInvariant()
            .Where(char.IsLetterOrDigit).ToArray());

    private static bool BrandRecognized(string name,string manufacturer,
        string brand)
    {
        var normalized=Normalize(name+" "+manufacturer);
        string own=Normalize(brand);
        if(normalized.Contains(own,StringComparison.Ordinal))return true;
        return brand switch{
            "WLMOUSE"=>normalized.Contains("wlmouse")||
                normalized.Contains("wlmouse",StringComparison.Ordinal),
            "ASUS"=>normalized.Contains("rog")||
                normalized.Contains("asustek"),
            "Logitech"=>normalized.Contains("logi"),
            "ATK"=>normalized.Contains("vxe"),
            "Sony"=>normalized.Contains("playstation")||
                normalized.Contains("dualsense"),
            "Microsoft"=>normalized.Contains("xbox"),
            "Nintendo"=>normalized.Contains("switchprocontroller"),
            _=>false
        };
    }

    internal static Match? Recognize(string name,string manufacturer,
        string transport)
    {
        if(string.IsNullOrWhiteSpace(name))return null;
        string normalized=Normalize(name);
        if(normalized.Length<3)return null;
        bool wireless=transport.Contains("Bluetooth",
            StringComparison.OrdinalIgnoreCase)||
            transport.Contains("USB",StringComparison.OrdinalIgnoreCase)||
            transport.Contains("2.4",StringComparison.OrdinalIgnoreCase)||
            transport.Contains("Wireless",StringComparison.OrdinalIgnoreCase);
        if(!wireless)return null;
        // Model matching must be anchored to the same recognized brand.
        // A generic USB receiver may match a family, never a specific model.
        var found=Items.Where(item=>
            BrandRecognized(name,manufacturer,item.Brand)&&
            Normalize(item.Model).Length>=2&&
            normalized.Contains(Normalize(item.Model),
                StringComparison.Ordinal))
            .OrderByDescending(item=>Normalize(item.Model).Length)
            .FirstOrDefault();
        if(found is not null)
            return new(found.Brand,found.Model,found.Category,
                found.ReaderStatus,"model-name");
        string[] brands=Items.Select(x=>x.Brand)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        string? recognized=brands.FirstOrDefault(brand=>
            BrandRecognized(name,manufacturer,brand));
        if(recognized is null)return null;
        // A USB-only product of a known manufacturer (e.g. an ASUS AIO
        // cooler) is not necessarily battery-powered or even wireless.
        bool explicitWireless=transport.Contains("Bluetooth",
            StringComparison.OrdinalIgnoreCase)||
            new[]{"wireless","receiver","dongle","2.4","kablosuz"}
                .Any(token=>name.Contains(token,
                    StringComparison.OrdinalIgnoreCase));
        if(!explicitWireless)return null;
        return new(recognized,null,"Wireless",
            "Cihaz ailesi tanındı; pil protokolü doğrulanmadı",
            "brand-only");
    }

    /// <summary>Public USB VID/PID identity facts with distinctive models.
    /// Never treat a shared dongle PID as unique proof of its paired mouse.
    /// </summary>
    internal static Match? RecognizeUsbIdentity(int vendorId,int productId)
    {
        static Match Exact(string brand,string model)=>new(brand,model,
            "Mouse",
            "Alıcı kimliği tanındı; canlı pil okuyucusu henüz doğrulanmadı",
            "documented-receiver");

        // Shared receiver labels are deliberately NOT promoted to an
        // unverified physical mouse model in the blind ATK hardware test.
        if(vendorId==0x373B&&productId==0x101A)
            return new("ATK",null,"Mouse",
                "CompX 0x04 live battery reader installed; query pending",
                "protocol-dongle");
        if(vendorId==0x373B&&productId==0x101B)
            return new("ATK",null,"Mouse",
                "Experimental CompX battery reader; needs hardware test",
                "protocol-dongle");
        if(vendorId==0x373B&&productId==0x10C9)
            return Exact("ATK","A9 Plus");
        if(vendorId==0x36A7)
            return productId switch{
                0xA870 or 0xA871=>Exact("WLMOUSE","Beast X Pro"),
                0xA880 or 0xA881=>Exact("WLMOUSE","Beast Max"),
                0xA883 or 0xA884 or 0xA887=>
                    Exact("WLMOUSE","Beast X"),
                0xA885 or 0xA886=>Exact("WLMOUSE","Beast Mini"),
                0xA878 or 0xA879=>Exact("WLMOUSE","Sword X"),
                _=>null
            };
        if(vendorId==0x3057)
            return productId switch{
                0x1003=>Exact("VAXEE","XE Wireless"),
                0x1004=>Exact("VAXEE","NP-01S Wireless"),
                0x1005=>Exact("VAXEE","AX Wireless"),
                0x1006=>Exact("VAXEE","NP-01 Wireless"),
                0x1007=>Exact("VAXEE","XE-S Wireless"),
                0x1008=>Exact("VAXEE","XE-S-L Wireless"),
                0x1009=>Exact("VAXEE","VAXEE x Ninjutso Sora"),
                0x1010=>Exact("VAXEE","E1 Wireless"),
                0x1011=>Exact("VAXEE","NP-01S V2 Wireless"),
                0x1012=>Exact("VAXEE","XE V2 Wireless"),
                0x1013=>Exact("VAXEE","NP-01S Ergo Wireless"),
                0x1014=>Exact("VAXEE","NP-01S V3 Wireless"),
                0x1015=>Exact("VAXEE","NP-01 Ergo Wireless"),
                _=>null
            };
        if(vendorId==0x046D)
            return productId switch{
                0xC094=>Exact("Logitech","G Pro X Superlight"),
                0xC095=>Exact("Logitech","G502 X Plus"),
                _=>null
            };
        return null;
    }

    internal static (int Vid,int Pid)? ReadHardwareId(string identity)
    {
        if(string.IsNullOrWhiteSpace(identity))return null;
        var match=Regex.Match(identity,
            @"VID_([0-9A-F]{4}).*PID_([0-9A-F]{4})",
            RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        if(!match.Success)return null;
        return (Convert.ToInt32(match.Groups[1].Value,16),
            Convert.ToInt32(match.Groups[2].Value,16));
    }
}
