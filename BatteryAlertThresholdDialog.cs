using System;
using System.Drawing;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

internal sealed class BatteryAlertThresholdDialog:Form
{
    private readonly DarkNumberBox threshold=new(){
        Left=170,Top=85,Width=93,Minimum=1,Maximum=99
    };
    public int Percent=20;
    public BatteryAlertThresholdDialog(string device,int current,
        BatterySnapshot? snapshot=null)
    {
        UiPolish.Register(this);
        Text=UiLanguage.English?"Device low battery alert":"Cihaz düşük pil uyarısı";
        ClientSize=new Size(390,186);
        FormBorderStyle=FormBorderStyle.FixedDialog;
        StartPosition=FormStartPosition.CenterParent;
        MaximizeBox=false;MinimizeBox=false;
        BackColor=Color.FromArgb(13,18,26);
        ForeColor=Color.FromArgb(236,247,253);
        Font=new Font("Segoe UI",10f);
        Controls.Add(new Label{
            Left=18,Top=19,Width=350,Height=40,
            Text=BatteryPriority.DisplayName(device,snapshot),
            Font=new Font("Segoe UI Semibold",12f,FontStyle.Bold)
        });
        Controls.Add(new Label{
            Left=18,Top=90,Width=144,Height=23,
            Text=UiLanguage.English?"Alert below (%):":"Uyarı eşiği (%):"
        });
        threshold.Value=Math.Clamp(current,1,99);
        Controls.Add(threshold);
        var ok=new DarkButton{
            Left=261,Top=132,Width=108,Height=35,
            Text=UiLanguage.English?"Save":"Kaydet"
        };
        ok.Click+=(_,_)=>{
            Percent=(int)threshold.Value;
            DialogResult=DialogResult.OK;
            Close();
        };
        Controls.Add(ok);
        AcceptButton=ok;
        UiSkin.ApplyDark(this);
    }
}
