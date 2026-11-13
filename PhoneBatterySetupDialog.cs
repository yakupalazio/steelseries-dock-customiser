using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

/// <summary>
/// User-initiated instructions for obtaining an accurate Android battery
/// reading without ever foregrounding Phone Link. Never enables debugging,
/// installs software, pairs devices or opens a shell implicitly.
/// </summary>
internal sealed class PhoneBatterySetupDialog : Form
{
    private readonly Label status=new(){
        Left=20,Top=250,Width=550,Height=65,
        ForeColor=Color.FromArgb(124,208,237)
    };
    private readonly bool adbAllowed;

    public PhoneBatterySetupDialog(bool currentlyAllowed)
    {
        UiPolish.Register(this);
        adbAllowed=currentlyAllowed;
        Text=UiLanguage.T("Telefon pilini arka planda oku");
        ClientSize=new Size(610,410);
        MinimumSize=new Size(610,410);
        StartPosition=FormStartPosition.CenterParent;
        BackColor=Color.FromArgb(19,26,36);
        ForeColor=Color.FromArgb(230,242,249);
        Font=new Font("Segoe UI",9.5f);
        Controls.Add(new Label{
            Left=20,Top=18,Width=560,Height=46,
            Text=UiLanguage.T("Galaxy S24+ için arka planda pil takibi"),
            Font=new Font("Segoe UI Semibold",16,FontStyle.Bold),
            ForeColor=Color.FromArgb(99,222,240)
        });
        Controls.Add(new Label{
            Left=20,Top=72,Width=565,Height=155,
            Text=UiLanguage.English?
                "Phone Link does not reliably expose a live battery value to third-party apps.\n\n"+
                "1. Install Google's official Android SDK Platform Tools.\n"+
                "2. Authorize your Galaxy S24+ over USB or pair Wireless debugging on a trusted Wi-Fi network.\n"+
                "3. Enable 'Android ADB battery source' in Battery settings, then Apply.\n"+
                "4. Return here to check the read-only battery connection.":
                "Phone Link, pil yüzdesini üçüncü taraflara güvenilir biçimde sunmuyor.\n\n"+
                "1. Google'ın resmî Android SDK Platform Tools paketini kur.\n"+
                "2. Galaxy S24+'ta USB hata ayıklamaya izin ver veya güvenilir Wi-Fi üzerinde kablosuz eşleştir.\n"+
                "3. Pil ayarlarında 'Android ADB pil kaynağı'nı işaretleyip uygula.\n"+
                "4. Salt-okunur bağlantı durumunu buradan kontrol et."
        });
        var guide=new DarkButton{
            Left=20,Top=334,Width=264,Height=38,
            Text=UiLanguage.English?"Official Platform Tools":"Resmî Platform Tools sayfası"};
        guide.Click+=(_,_)=>Process.Start(new ProcessStartInfo(
            "https://developer.android.com/tools/releases/platform-tools"){
                UseShellExecute=true
            });
        Controls.Add(guide);
        var check=new DarkButton{
            Left=297,Top=334,Width=130,Height=38,
            Text=UiLanguage.English?"Check battery":"Pili kontrol et"};
        check.Click+=async (_,_)=>await CheckBatteryAsync(check);
        Controls.Add(check);
        var close=new DarkButton{
            Left=447,Top=334,Width=138,Height=38,
            Text=UiLanguage.English?"Close":"Kapat"};
        close.Click+=(_,_)=>Close();
        Controls.Add(close);
        Controls.Add(status);
        ShowStatus();
        UiSkin.ApplyDark(this);
    }

    private void ShowStatus()
    {
        status.Text=!AdbPhoneBatteryProvider.Installed
            ?(UiLanguage.English?
                "ADB not installed. Phone will show no percentage until a verified background source is available.":
                "ADB kurulu değil. Doğrulanmış arka plan kaynağı olmadan telefon yüzdesi gösterilmeyecek.")
            :!adbAllowed
                ?(UiLanguage.English?
                    "ADB found, but the opt-in battery source is disabled in settings.":
                    "ADB bulundu ancak pil ayarlarındaki isteğe bağlı kaynak kapalı.")
                :(UiLanguage.English?
                    "ADB found and enabled. Click Check battery to verify your Galaxy.":
                    "ADB bulundu ve etkin. Galaxy bağlantısını doğrulamak için Pili kontrol et.");
    }

    private async Task CheckBatteryAsync(Button button)
    {
        button.Enabled=false;
        try
        {
            if(!AdbPhoneBatteryProvider.Installed || !adbAllowed)
            {
                ShowStatus();
                return;
            }
            var reading=await AdbPhoneBatteryProvider.ReadAsync();
            status.Text=reading?.Available==true
                ?(UiLanguage.English?"Verified Galaxy S24+ battery: ":"Doğrulanan Galaxy S24+ pil seviyesi: ")+reading.Display
                :(UiLanguage.English?
                    "No authorized S24+ connection found. Check USB permission / Wi-Fi pairing.":
                    "Yetkilendirilmiş S24+ bulunamadı. USB izni veya Wi-Fi eşleştirmesini kontrol et.");
        }
        catch(Exception ex)
        {
            status.Text=(UiLanguage.English?"Read failed: ":"Pil okunamadı: ")+ex.GetType().Name;
        }
        finally{button.Enabled=true;}
    }
}
