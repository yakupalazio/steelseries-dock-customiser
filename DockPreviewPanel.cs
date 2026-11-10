using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Photo-based simulation only. Does not enumerate USB devices, use GameSense,
/// or attempt to read the dock's physical framebuffer.
/// </summary>
internal sealed class DockPreviewPanel : UserControl
{
    private sealed class PhotoSettings
    {
        public string? ImagePath { get; set; }
        public List<PointF> Corners { get; set; } = [];
        public int Brightness { get; set; } = 92;
    }

    private readonly Func<Bitmap> _frameFactory;
    private readonly PictureBox _photoBox = new()
    {
        Left = 16, Top = 104, Width = 770, Height = 482,
        BackColor = Color.FromArgb(18,22,30), BorderStyle = BorderStyle.FixedSingle
    };
    private readonly RadioButton _custom = new() { Text = "Custom (canlı tasarım)", Checked = true, AutoSize = true };
    private readonly RadioButton _factory = new() { Text = "SteelSeries Default (fotoğraf)", AutoSize = true };
    private readonly Button _choose = new() { Text = "Fotoğraf seç", Width = 114, Height = 31 };
    private readonly Button _restore = new() { Text = "Örnek fotoğrafa dön", Width = 154, Height = 31 };
    private readonly Button _calibrate = new() { Text = "Ekranı hizala", Width = 119, Height = 31 };
    private readonly Button _cancelCalibration = new() { Text = "İptal", Width = 70, Height = 31, Enabled = false };
    private readonly Label _message = new() { Left=16,Top=642,Width=775,Height=76,
        Text = "Bu sekme tasarımın fotoğraf üzerindeki ANLIK simülasyonudur. Gerçek dock ekranını okumaz veya değiştirmez.",
        ForeColor=Color.FromArgb(42,90,75)
    };

    private readonly string _assetDefault = Path.Combine(AppContext.BaseDirectory, "dock-photo-local.png");
    private readonly string _settingsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SteelseriesDockCustomiser","dock-preview-v07.json");
    private Image? _photo;
    private PhotoSettings _settings = new();
    private bool _calibrating;
    private readonly List<PointF> _newCorners = [];
    private RectangleF _imageRect;
    private readonly PointF[] _factoryCorners = [
        new(346,301),new(589,302),new(589,420),new(346,419)
    ];

    public DockPreviewPanel(Func<Bitmap> frameFactory)
    {
        _frameFactory = frameFactory;
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(245,247,249);

        Controls.Add(new Label { Left=16,Top=14,Width=777,Height=30,
            Font=new Font("Segoe UI",15,FontStyle.Bold), Text="Dock Preview — gerçek ürün fotoğrafında önizleme" });
        var row = new FlowLayoutPanel { Left=16,Top=50,Width=770,Height=42,WrapContents=false };
        row.Controls.AddRange([_custom,_factory]);
        Controls.Add(row);
        Controls.Add(_photoBox);

        var options = new FlowLayoutPanel { Left=16,Top=592,Width=770,Height=46,WrapContents=false,AutoScroll=true };
        options.Controls.AddRange([_choose,_restore,_calibrate,_cancelCalibration]);
        Controls.Add(options);
        Controls.Add(_message);

        _photoBox.Paint += (_,e) => PaintPhoto(e.Graphics);
        _photoBox.MouseClick += PhotoClick;
        _custom.CheckedChanged += (_,_) => InvalidateFrame();
        _factory.CheckedChanged += (_,_) => InvalidateFrame();
        _choose.Click += (_,_) => ChoosePhoto();
        _restore.Click += (_,_) => RestoreExample();
        _calibrate.Click += (_,_) => StartCalibration();
        _cancelCalibration.Click += (_,_) => EndCalibration(false);

        LoadSettings();
        LoadImage();
    }

    public void InvalidateFrame() => _photoBox.Invalidate();

    private void RestoreExample()
    {
        _settings.ImagePath=null;
        _settings.Corners=_factoryCorners.ToList();
        SaveSettings();
        LoadImage();
        _message.Text="Resmî SteelSeries ürün fotoğrafı yüklendi. Fotoğraf yalnızca yerel önizleme içindir.";
    }

    private void ChoosePhoto()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Görüntüler|*.png;*.jpg;*.jpeg;*.bmp;*.webp|Tüm dosyalar|*.*",
            Title = "Kendi dock fotoğrafını seç"
        };
        if(dialog.ShowDialog()!=DialogResult.OK)return;
        try
        {
            // Validate file before copying into user-local preview cache.
            using(var image = Image.FromFile(dialog.FileName))
                if(image.Width>6000 || image.Height>6000)
                    throw new InvalidDataException("Fotoğraf 6000x6000 pikselle sınırlıdır.");
            var folder=Path.GetDirectoryName(_settingsFile)!;
            Directory.CreateDirectory(folder);
            string extension=Path.GetExtension(dialog.FileName).ToLowerInvariant();
            string saved=Path.Combine(folder,"user-dock-photo"+extension);
            if(!Path.GetFullPath(dialog.FileName).Equals(Path.GetFullPath(saved),StringComparison.OrdinalIgnoreCase))
                File.Copy(dialog.FileName,saved,true);
            _settings.ImagePath=saved;
            _settings.Corners=[];
            SaveSettings();
            LoadImage();
            _message.Text="Fotoğraf yüklendi. Şimdi 'Ekranı hizala' ile OLED'in dört köşesini işaretle.";
        }
        catch(Exception ex){ _message.Text="Fotoğraf yüklenemedi: "+ex.Message; }
    }

    private void LoadSettings()
    {
        try
        {
            if(File.Exists(_settingsFile))
                _settings=JsonSerializer.Deserialize<PhotoSettings>(File.ReadAllText(_settingsFile))??new PhotoSettings();
        }
        catch { _settings=new PhotoSettings(); }
    }

    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsFile)!);
            File.WriteAllText(_settingsFile,JsonSerializer.Serialize(_settings));
        }
        catch { }
    }

    private void LoadImage()
    {
        var path = _settings.ImagePath ?? _assetDefault;
        try
        {
            if(!File.Exists(path))throw new FileNotFoundException("Fotoğraf bulunamadı.",path);
            using var image = Image.FromFile(path);
            var clone = new Bitmap(image);
            var old=_photo;
            _photo=clone;
            old?.Dispose();
            if(_settings.Corners.Count!=4 && _settings.ImagePath==null)
                _settings.Corners=_factoryCorners.ToList();
            InvalidateFrame();
        }
        catch(Exception ex)
        {
            _message.Text="Dock fotoğrafı yüklenemedi: "+ex.Message+
                "  'Fotoğraf seç' ile kendi fotoğrafını yükleyebilirsin.";
        }
    }

    private void StartCalibration()
    {
        if(_photo==null)return;
        _calibrating=true;
        _newCorners.Clear();
        _cancelCalibration.Enabled=true;
        _photoBox.Cursor=Cursors.Cross;
        _message.Text="Ekranı sırayla işaretle: 1) sol üst, 2) sağ üst, 3) sağ alt, 4) sol alt. Orijinal batarya simgeleri korunacak.";
        InvalidateFrame();
    }

    private void EndCalibration(bool save)
    {
        _calibrating=false;
        _cancelCalibration.Enabled=false;
        _photoBox.Cursor=Cursors.Default;
        if(save && _newCorners.Count==4)
        {
            _settings.Corners=_newCorners.ToList();
            SaveSettings();
            _message.Text="OLED perspektifi kaydedildi. Tasarım şimdi işaretlediğin alana yerleşiyor.";
        }
        else _message.Text="Hizalama iptal edildi; önceki ekran koordinatları korundu.";
        _newCorners.Clear();
        InvalidateFrame();
    }

    private void PhotoClick(object? sender, MouseEventArgs e)
    {
        if(!_calibrating||_photo==null||!_imageRect.Contains(e.Location))return;
        var x=(e.X-_imageRect.X)/_imageRect.Width*_photo.Width;
        var y=(e.Y-_imageRect.Y)/_imageRect.Height*_photo.Height;
        _newCorners.Add(new PointF(x,y));
        if(_newCorners.Count==4)EndCalibration(true);
        else _message.Text="Köşe "+_newCorners.Count+"/4 işaretlendi. Sonraki köşe: "+
            new[]{"sol üst","sağ üst","sağ alt","sol alt"}[_newCorners.Count];
        InvalidateFrame();
    }

    private void PaintPhoto(Graphics g)
    {
        g.Clear(Color.FromArgb(25,29,37));
        if(_photo==null)return;

        _imageRect=FitToBox(_photo.Size,_photoBox.ClientRectangle);
        using var backgroundBrush = new LinearGradientBrush(
            new Point(0,0),new Point(0,_photoBox.Height),
            Color.FromArgb(49,52,61),Color.FromArgb(24,27,34));
        g.FillRectangle(backgroundBrush,_photoBox.ClientRectangle);
        g.SmoothingMode=SmoothingMode.HighQuality;
        g.InterpolationMode=InterpolationMode.HighQualityBicubic;

        if(_factory.Checked||_settings.Corners.Count!=4)
        {
            g.DrawImage(_photo,_imageRect);
        }
        else
        {
            using var render=_frameFactory();
            using var combined=ComposePhotograph(_photo,render,_settings.Corners,92);
            g.DrawImage(combined,_imageRect);
        }

        if(_calibrating)
        {
            using var p=new Pen(Color.FromArgb(80,220,235),2);
            for(int i=0;i<_newCorners.Count;i++)
            {
                var pt=ToScreen(_newCorners[i]);
                g.FillEllipse(Brushes.Cyan,pt.X-5,pt.Y-5,10,10);
                if(i>0)g.DrawLine(p,ToScreen(_newCorners[i-1]),pt);
            }
        }
    }

    private PointF ToScreen(PointF pt) =>
        new(_imageRect.Left+pt.X/_photo!.Width*_imageRect.Width,
            _imageRect.Top+pt.Y/_photo.Height*_imageRect.Height);

    private static RectangleF FitToBox(Size image,Rectangle box)
    {
        float scale=Math.Min((float)box.Width/image.Width,(float)box.Height/image.Height);
        float w=image.Width*scale,h=image.Height*scale;
        return new((box.Width-w)/2,(box.Height-h)/2,w,h);
    }

    /// <summary>
    /// Keeps the actual glass appearance of a photographed OLED display.
    /// Reconstructs the dark off-pixel background from dark, nearby photo pixels,
    /// removes stale factory text beneath the custom area, feathers edges, and
    /// blends native-pixel glyphs in photo space. Never affects the actual dock.
    /// </summary>
    public static Bitmap ComposePhotograph(Image photo, Bitmap oled,
        IReadOnlyList<PointF> corners,int brightness)
    {
        var output=new Bitmap(photo.Width,photo.Height,PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(output))g.DrawImage(photo,0,0,photo.Width,photo.Height);
        if(corners.Count!=4)return output;

        var map=PerspectiveMap.Create(corners);
        if(map==null)return output;
        int x0=Math.Max(0,(int)Math.Floor(corners.Min(p=>p.X)));
        int x1=Math.Min(output.Width-1,(int)Math.Ceiling(corners.Max(p=>p.X)));
        int y0=Math.Max(0,(int)Math.Floor(corners.Min(p=>p.Y)));
        int y1=Math.Min(output.Height-1,(int)Math.Ceiling(corners.Max(p=>p.Y)));
        if(x1<=x0 || y1<=y0)return output;

        // Compute a dark neutral background for every source-photo row. The
        // bright original screen characters are excluded from the estimation.
        var background=new Color[output.Height];
        for(int row=y0;row<=y1;row++)
        {
            var rr=new List<byte>(); var gg=new List<byte>(); var bb=new List<byte>();
            for(int col=x0;col<=x1;col++)
            {
                var c=output.GetPixel(col,row);
                if(c.R<72 && c.G<72 && c.B<72)
                {
                    rr.Add(c.R);gg.Add(c.G);bb.Add(c.B);
                }
            }
            if(rr.Count==0){background[row]=Color.FromArgb(23,24,26);continue;}
            rr.Sort();gg.Sort();bb.Sort();
            int mid=rr.Count/2;
            background[row]=Color.FromArgb(rr[mid],gg[mid],bb[mid]);
        }

        // Smooth the source-photo dark-glass background vertically; this avoids
        // reproducing the old stock display's text as horizontal dark bands.
        var smooth=new Color[output.Height];
        for(int row=y0;row<=y1;row++)
        {
            int rr=0,gg=0,bb=0,count=0;
            for(int near=Math.Max(y0,row-10);near<=Math.Min(y1,row+10);near++)
            {
                rr+=background[near].R;gg+=background[near].G;bb+=background[near].B;count++;
            }
            smooth[row]=Color.FromArgb(rr/count,gg/count,bb/count);
        }

        double white=222.0 + 0.24*Math.Clamp(brightness,35,100);
        for(int y=y0;y<=y1;y++)
        for(int x=x0;x<=x1;x++)
        {
            if(!map.TryInverse(x+.5,y+.5,out var u,out var v))continue;
            if(u<0 || u>=1 || v<0 || v>=1)continue;
            double nx=u*128,ny=v*64;
            if(ny<12)continue; // Factory battery icons remain photo pixels.

            var old=output.GetPixel(x,y);
            var bg=smooth[y];

            // Sample in native pixel coordinates with fractional coverage:
            // camera-like edges without inventing extra OLED resolution.
            double a=NativeCoverage(oled,nx-.5,ny-.5);

            double distance=Math.Min(
                Math.Min(nx,128-nx),
                Math.Min(ny-12,64-ny));
            double edge=Math.Clamp(distance/3.0,0.0,1.0);
            // Restrained photo texture, clamped so that original white text
            // cannot leak through the new black pixels.
            double noiseR=Math.Min(old.R,bg.R+7)*.08;
            double noiseG=Math.Min(old.G,bg.G+7)*.08;
            double noiseB=Math.Min(old.B,bg.B+7)*.08;
            double offR=bg.R*.92+noiseR;
            double offG=bg.G*.92+noiseG;
            double offB=bg.B*.92+noiseB;
            double r=offR + (white-offR)*a;
            double g=offG + (white+3-offG)*a;
            double b=offB + (white+4-offB)*a;

            // Blend at screen boundaries so no pasted-on black rectangle is visible.
            output.SetPixel(x,y,Color.FromArgb(255,
                (int)Math.Clamp(old.R*(1-edge)+r*edge,0,255),
                (int)Math.Clamp(old.G*(1-edge)+g*edge,0,255),
                (int)Math.Clamp(old.B*(1-edge)+b*edge,0,255)));
        }
        return output;
    }

    private static double NativeCoverage(Bitmap oled,double x,double y)
    {
        int x0=(int)Math.Floor(x), y0=(int)Math.Floor(y);
        double fx=x-x0,fy=y-y0;
        double a=IsLit(oled,x0,y0)*(1-fx)+IsLit(oled,x0+1,y0)*fx;
        double b=IsLit(oled,x0,y0+1)*(1-fx)+IsLit(oled,x0+1,y0+1)*fx;
        return a*(1-fy)+b*fy;
    }

    private static int IsLit(Bitmap image,int x,int y) =>
        x>=0 && x<128 && y>=12 && y<64 && image.GetPixel(x,y).R>127 ? 1 : 0;

    protected override void Dispose(bool disposing)
    {
        if(disposing)_photo?.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>8-parameter perspective transform using exact quad-to-rect mapping.</summary>
internal sealed class PerspectiveMap
{
    private readonly double[] h;
    private PerspectiveMap(double[] transform) => h=transform;

    public static PerspectiveMap? Create(IReadOnlyList<PointF> corners)
    {
        if(corners.Count!=4)return null;
        var uv=new (double u,double v)[]{(0,0),(1,0),(1,1),(0,1)};
        var a=new double[8,9];
        for(int i=0;i<4;i++)
        {
            double u=uv[i].u,v=uv[i].v,x=corners[i].X,y=corners[i].Y;
            int r=2*i;
            a[r,0]=u;a[r,1]=v;a[r,2]=1;a[r,6]=-u*x;a[r,7]=-v*x;a[r,8]=x;
            a[r+1,3]=u;a[r+1,4]=v;a[r+1,5]=1;a[r+1,6]=-u*y;a[r+1,7]=-v*y;a[r+1,8]=y;
        }
        for(int col=0;col<8;col++)
        {
            int pivot=col;
            for(int row=col+1;row<8;row++)
                if(Math.Abs(a[row,col])>Math.Abs(a[pivot,col]))pivot=row;
            if(Math.Abs(a[pivot,col])<1e-10)return null;
            for(int c=col;c<9;c++)(a[col,c],a[pivot,c])=(a[pivot,c],a[col,c]);
            var div=a[col,col];
            for(int c=col;c<9;c++)a[col,c]/=div;
            for(int row=0;row<8;row++)if(row!=col)
            {
                var k=a[row,col];
                for(int c=col;c<9;c++)a[row,c]-=k*a[col,c];
            }
        }
        var h=new double[8];
        for(int i=0;i<8;i++)h[i]=a[i,8];
        return new PerspectiveMap(h);
    }

    public bool TryInverse(double x,double y,out double u,out double v)
    {
        double a=x*h[6]-h[0],b=x*h[7]-h[1],c=h[2]-x;
        double d=y*h[6]-h[3],e=y*h[7]-h[4],f=h[5]-y;
        double det=a*e-b*d;
        if(Math.Abs(det)<1e-12){u=v=0;return false;}
        u=(c*e-b*f)/det;v=(a*f-c*d)/det;
        return !double.IsNaN(u) && !double.IsNaN(v);
    }
}
