using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SteelseriesDockCustomiser;

/// <summary>
/// SteelSeries GG Engine's local, undocumented READ-ONLY API.
/// Resolves the active loadout and Engine port dynamically.
/// Performs GET requests only to the loopback address.
/// Sends NO USB/HID/device commands and makes NO settings changes.
/// </summary>
internal static class GgBatteryProvider
{
    private static readonly Uri Core = new("https://127.0.0.1:6327/");
    private static readonly HashSet<string> ExpectedNames = new(StringComparer.Ordinal)
    {
        "aerox_3_wireless_gen2",
        "arctis_nova_pro_wireless_tx"
    };

    public static async Task<Dictionary<string,BatteryReading>> ReadAsync()
    {
        var found=new Dictionary<string,BatteryReading>(StringComparer.OrdinalIgnoreCase);
        using var handler = new HttpClientHandler
        {
            AllowAutoRedirect=false,
            // GG uses a dynamically generated self-signed certificate.
            // Trust it ONLY for this dedicated local GG client; never globally.
            ServerCertificateCustomValidationCallback=(message,_,_,_) =>
                message.RequestUri is { Scheme:"https", Host:"127.0.0.1" }
        };
        using var client=new HttpClient(handler){ Timeout=TimeSpan.FromSeconds(4) };
        try
        {
            using var apps=await ReadJson(client,new Uri(Core,"subApps"));
            if(!apps.RootElement.TryGetProperty("subApps",out var subs) ||
               !subs.TryGetProperty("engine",out var engine) ||
               !engine.TryGetProperty("metadata",out var metadata) ||
               !metadata.TryGetProperty("encryptedWebServerAddress",out var address))
                return found;
            string? rawAddress=address.GetString();
            if(!TryEngineUri(rawAddress,out Uri? baseUri) || baseUri is null)return found;

            using var active=await ReadJson(client,new Uri(Core,"v1/loadouts/active"));
            if(!active.RootElement.TryGetProperty("activeLoadout",out var loadout) ||
               !loadout.TryGetProperty("id",out var idProp))
                return found;
            string id=idProp.GetString()??"";
            if(!Guid.TryParse(id,out _))return found;

            using var devices=await ReadJson(client,new Uri(baseUri,"devices"));
            if(!devices.RootElement.TryGetProperty("devices",out var deviceArr) ||
               deviceArr.ValueKind!=JsonValueKind.Array)return found;
            foreach(var device in deviceArr.EnumerateArray())
            {
                if(!device.TryGetProperty("name",out var nameProp))continue;
                string name=nameProp.GetString()??"";
                if(!ExpectedNames.Contains(name) ||
                   !device.TryGetProperty("connected",out var connected) ||
                   !IsConnected(connected))continue;

                string relative="v2/device/config/loadout?device="+
                    Uri.EscapeDataString(name)+"&loadoutId="+Uri.EscapeDataString(id);
                try
                {
                    using var state=await ReadJson(client,new Uri(baseUri,relative));
                    if(!state.RootElement.TryGetProperty("loadoutConfiguration",out var config) ||
                       !config.TryGetProperty("statuses",out var statuses) ||
                       !statuses.TryGetProperty("batteryLevels",out var battery))
                        continue;
                    if(!TryParseBattery(name,battery,DateTimeOffset.Now,out var reading))continue;
                    found[reading.Kind]=reading;
                    if(TryParseSpareBattery(name,battery,DateTimeOffset.Now,out var spare))
                        found["SpareBattery"]=spare;
                }
                catch(HttpRequestException){}
                catch(TaskCanceledException){}
                catch(JsonException){}
            }
        }
        catch(HttpRequestException){}
        catch(TaskCanceledException){}
        catch(JsonException){}
        catch(InvalidOperationException){}
        return found;
    }

    internal static bool TryEngineUri(string? text,out Uri? uri)
    {
        uri=null;
        if(string.IsNullOrWhiteSpace(text) ||
           !Uri.TryCreate("https://"+text,UriKind.Absolute,out var candidate) ||
           candidate.Host!="127.0.0.1" ||
           candidate.Port is <1024 or >65535 ||
           candidate.AbsolutePath!="/" ||
           candidate.Query.Length>0 ||
           !string.IsNullOrEmpty(candidate.UserInfo))
            return false;
        uri=candidate;
        return true;
    }

    internal static bool TryParseBattery(string device,JsonElement b,
        DateTimeOffset at,out BatteryReading reading)
    {
        reading=new BatteryReading("Unknown",null,"SteelSeries GG Engine",
            "Geçersiz durum",at);
        string kind=device switch
        {
            "aerox_3_wireless_gen2"=>"Mouse",
            "arctis_nova_pro_wireless_tx"=>"Headset",
            _=>""
        };
        if(string.IsNullOrEmpty(kind) ||
           !b.TryGetProperty("type",out var typeElement) ||
           typeElement.ValueKind!=JsonValueKind.String)
            return false;

        var type=typeElement.GetString();
        bool known=type switch
        {
            "single_battery"=>GetBool(b,"statusKnown"),
            "primary_and_backup_battery"=>GetBool(b,"primaryStatusKnown"),
            _=>false
        };
        if(!known || !TryPercent(b,"percent",out int pct))return false;

        bool charging=GetBool(b,"charging");
        string detail=charging?"Şarj oluyor":"Bağlı";
        if(type=="primary_and_backup_battery" &&
           TryPercent(b,"backupPercent",out int backup))
        {
            detail+=$" | Dock'taki yedek pil: {backup}%";
            if(GetBool(b,"backupCharging"))detail+=" (şarj oluyor)";
        }

        reading=new BatteryReading(kind,pct,"SteelSeries GG Engine (canlı)",
            detail,at);
        return true;
    }

    internal static bool TryParseSpareBattery(string device,JsonElement b,
        DateTimeOffset at,out BatteryReading reading)
    {
        reading=new BatteryReading("SpareBattery",null,"SteelSeries GG","Yedek pil bilgisi yok",at);
        if(device!="arctis_nova_pro_wireless_tx" ||
           !b.TryGetProperty("type",out var type) ||
           type.GetString()!="primary_and_backup_battery" ||
           !TryPercent(b,"backupPercent",out var percent))
            return false;
        if(b.TryGetProperty("backupStatusKnown",out var known) &&
           known.ValueKind==JsonValueKind.False)return false;
        reading=new BatteryReading("SpareBattery",percent,
            "SteelSeries GG Engine (canlı)",
            GetBool(b,"backupCharging")?"Dock'ta şarj oluyor":"Yedek pil takılı",at);
        return true;
    }

    private static bool TryPercent(JsonElement o,string name,out int percent)
    {
        percent=0;
        return o.TryGetProperty(name,out var val) &&
               val.ValueKind==JsonValueKind.Number &&
               val.TryGetInt32(out percent) && percent is >=0 and <=100;
    }

    private static bool GetBool(JsonElement o,string name) =>
        o.TryGetProperty(name,out var val) && val.ValueKind==JsonValueKind.True;

    private static bool IsConnected(JsonElement value) =>
        value.ValueKind==JsonValueKind.True ||
        (value.ValueKind==JsonValueKind.Number &&
         value.TryGetInt32(out int i) && i==1);

    private static async Task<JsonDocument> ReadJson(HttpClient client,Uri uri)
    {
        if(uri.Host!="127.0.0.1" || uri.Scheme!="https")
            throw new InvalidOperationException("Only GG loopback HTTPS is allowed.");
        using var response=await client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        if(response.Content.Headers.ContentLength is long len && len>256000)
            throw new InvalidOperationException("GG response too large.");
        using var stream=await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream,new JsonDocumentOptions
        {
            MaxDepth=32
        });
    }
}
