using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Secret original 128x64 monochrome ray-casting mini FPS. The PC window shows
/// instructions only; gameplay frames are streamed solely to the dock after
/// explicit consent. No DOOM engine or copyrighted assets are used.
/// </summary>
internal sealed class DoomModeForm : Form
{
    private const int OledWidth = 128, OledHeight = 64;
    private const double FieldOfView = Math.PI * 0.43;
    private readonly string[] maze =
    [
        "############",
        "#..........#",
        "#..#.......#",
        "#..#..##...#",
        "#..........#",
        "#....#.....#",
        "#..........#",
        "#...###....#",
        "#..........#",
        "#...#......#",
        "#..........#",
        "############"
    ];
    private readonly HashSet<Keys> held = [];
    private readonly PictureBox view = new()
    {
        Dock = DockStyle.Fill, BackColor = Color.Black,
        SizeMode = PictureBoxSizeMode.Normal
    };
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 40 };
    private readonly Bitmap frame = new(OledWidth, OledHeight);
    private readonly Button hardwareButton = new DarkButton()
    {
        Text = "▶ DOCK'TA CANLI OYNA",
        Width = 254, Height = 33,
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.FromArgb(38, 68, 82),
        ForeColor = Color.FromArgb(106, 211, 244),
        Cursor = Cursors.Hand
    };
    private readonly Func<bool> otherOledWriterActive;
    private readonly bool autoStart;
    private readonly DockHardwareTest restore = new();
    private FastOledTransport? physicalTransport;
    private bool physicalActive;
    private int sendingFrame, frameCounter;
    private readonly Label status = new()
    {
        Dock = DockStyle.Bottom, Height = 44,
        TextAlign = ContentAlignment.MiddleCenter,
        BackColor = Color.FromArgb(19, 26, 37),
        ForeColor = Color.FromArgb(175, 195, 211),
        Text = "W/S hareket  •  A/D / oklar dön  •  SPACE ateş  •  R sıfırla  •  ESC çıkış"
    };
    private double playerX, playerY, heading, enemyX, enemyY;
    private int health, kills, respawnFrames, damageCooldown, flashFrames;
    private bool enemyAlive;

    public DoomModeForm(Func<bool>? otherOledWriterActive = null,bool autoStart = false)
    {
        this.otherOledWriterActive = otherOledWriterActive ?? (() => false);
        this.autoStart=autoStart;
        Text = "DOOM MODE — Secret OLED Mini FPS";
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(13, 18, 26);
        ForeColor = Color.White;
        ClientSize = new Size(860, 590);
        MinimumSize = new Size(550, 430);
        KeyPreview = true;
        DoubleBuffered = true;

        var title = new Label
        {
            Dock = DockStyle.Top, Height = 53,
            Text = "DOOM MODE     /     128 × 64",
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.FromArgb(19, 26, 37),
            ForeColor = Color.FromArgb(232, 242, 250),
            Font = new Font("Consolas", 17f, FontStyle.Bold)
        };
        var disclaimer = new Label
        {
            Dock = DockStyle.Bottom, Height = 32,
            Text = "Oyun görüntüsü yalnızca dock üzerinde görünür · ESC güvenli çıkış",
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.FromArgb(19, 26, 37),
            ForeColor = Color.FromArgb(113, 169, 184),
            Font = new Font("Segoe UI", 9f)
        };
        var hardwareRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = 45, WrapContents = false,
            BackColor = Color.FromArgb(19, 26, 37),
            Padding = new Padding(12, 3, 0, 0)
        };
        hardwareButton.FlatAppearance.BorderSize = 0;
        hardwareButton.TabStop = false;
        hardwareRow.Controls.Add(hardwareButton);
        Controls.Add(view);
        Controls.Add(disclaimer);
        Controls.Add(status);
        Controls.Add(hardwareRow);
        Controls.Add(title);
        view.Paint += PaintView;
        timer.Tick += (_, _) =>
        {
            // Never render game frames onto the PC screen.
            // Simulation pauses when the physical OLED stream is stopped.
            if(!physicalActive)return;
            UpdateGame();
            DrawGame();
            if(++frameCounter % 2 == 0)QueuePhysicalFrame();
            status.Text=UiLanguage.T("DOCK CANLI · W/S hareket  A/D dön  SPACE ateş  R yeniden başla  ESC çıkış");
        };
        hardwareButton.Click += (_, _) =>
        {
            TogglePhysical();
            ActiveControl = null;
            Focus();
        };
        KeyDown += HandleKeyDown;
        KeyUp += (_, e) => held.Remove(e.KeyCode);
        Deactivate += (_, _) =>
        {
            held.Clear();
            // Leaving the game also closes its exclusive OLED session,
            // allowing the caller to resume its previously-live layout.
            if(physicalActive && IsHandleCreated)
                BeginInvoke(new Action(Close));
        };
        Shown += (_, _) =>
        {
            ActiveControl=null;Focus();timer.Start();
            if(this.autoStart)
                BeginInvoke(new Action(()=>StartPhysical(skipConsent:true)));
        };
        FormClosing += (_, _) => StopPhysical();
        FormClosed += (_, _) => timer.Stop();
        ResetGame();
        DrawGame();
        UiLanguage.ApplyTree(this);
    }

    internal Bitmap SnapshotForTest() => new(frame);

    private void TogglePhysical()
    {
        if (physicalActive) { Close(); return; }
        StartPhysical(skipConsent:false);
    }

    private void StartPhysical(bool skipConsent)
    {
        if(physicalActive)return;
        if (otherOledWriterActive())
        {
            MessageBox.Show(this,
                UiLanguage.English?"Stop the main Dock Live mode first.":
                    "Önce ana penceredeki Dock Canlı modunu durdur.",
                UiLanguage.English?"OLED in use":"OLED zaten kullanılıyor", MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }
        string restorationCheck = restore.Validate();
        string deviceCheck = FastOledTransport.ValidateAvailable();
        if (restorationCheck.Length != 0 || deviceCheck.Length != 0)
        {
            MessageBox.Show(this,
                restorationCheck.Length != 0 ? restorationCheck : deviceCheck,
                UiLanguage.English?"OLED connection unavailable":"Güvenli OLED testi yapılamıyor", MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }
        string consent=UiLanguage.English
            ?"The experimental 128×64 game temporarily replaces the entire OLED, "+
                "including the OEM battery strip. No firmware or brightness settings are changed.\n\n"+
                "There is no time limit. Avoid leaving an unchanged OLED frame on for long periods.\n"+
                "ESC or closing the window restores the SteelSeries screen. Continue?"
            :"Deneysel 128×64 oyun aktarımı Nova Pro Wireless OLED'in TÜM ekranını " +
            "geçici olarak değiştirecek (orijinal üst pil şeridi dahil).\n\n" +
            "Yalnızca doğrulanan HID çizim komutları gönderilir; firmware ve " +
            "parlaklık değiştirilmez. Otomatik süre sınırı yoktur; " +
            "OLED yanığına karşı uzun süre aynı görüntüde bırakma.\n\n" +
            "ESC, Durdur veya pencereyi kapatma ardından doğrulanmış ggoled aracı " +
            "ile SteelSeries'in orijinal ekranına dönüş komutu gönderilir. " +
            "GG Now Playing açıkken çakışabilir.\n\n" +
            "Gerçek cihaza gönderilsin mi?";
        if (!skipConsent && MessageBox.Show(this,consent,
            UiLanguage.English?"DOOM MODE — experimental OLED":"DOOM MODE — deneysel donanım testi",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        try
        {
            var transport = new FastOledTransport();
            transport.Open();
            physicalTransport = transport;
            physicalActive = true;
            frameCounter = 0;
            hardwareButton.Text = UiLanguage.T("■ DURDUR / OLED RESET");
            QueuePhysicalFrame();
        }
        catch (Exception ex)
        {
            physicalTransport?.Dispose();
            physicalTransport = null;
            physicalActive = false;
            MessageBox.Show(this, (UiLanguage.English?"OLED connection failed: ":
                "OLED bağlantısı açılamadı: ") + ex.Message,
                "DOOM MODE", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void QueuePhysicalFrame()
    {
        if (!physicalActive || physicalTransport is null ||
            Interlocked.CompareExchange(ref sendingFrame, 1, 0) != 0)
            return;
        var bitmap = new Bitmap(frame);
        var transport = physicalTransport!;
        _ = Task.Run(() =>
        {
            try { transport.SendFrame(bitmap); }
            catch (Exception ex)
            {
                try
                {
                    if (!IsDisposed && IsHandleCreated)
                        BeginInvoke(new Action(() => StopPhysical(ex.Message)));
                }
                catch { /* Form closed; FormClosing already restores OEM. */ }
            }
            finally
            {
                bitmap.Dispose();
                Interlocked.Exchange(ref sendingFrame, 0);
            }
        });
    }

    private void StopPhysical(string? error = null)
    {
        if (!physicalActive && physicalTransport is null) return;
        physicalActive = false;
        hardwareButton.Text = UiLanguage.T("▶ DOCK'TA CANLI OYNA");
        // Drain a queued HID frame before releasing the stream and running
        // the OEM restore command. A late game frame must never overwrite
        // the ordinary display after the caller resumes its live loop.
        if(!SpinWait.SpinUntil(()=>Volatile.Read(ref sendingFrame)==0,
            TimeSpan.FromSeconds(3)))
            error??="OLED frame drain timed out";
        try { physicalTransport?.Dispose(); }
        catch (Exception ex) { error ??= ex.Message; }
        finally { physicalTransport = null; }
        // This bounded, SHA-verified CLI 'return' path is also used by the
        // existing on-demand physical test. ESC must not leave a custom frame.
        string restored = restore.RestoreNow();
        status.Text = UiLanguage.T((error is null ? "" : "OLED aktarım hatası: " + error + " | ") +
            restored + "   |   ESC: çıkış");
    }

    private void ResetGame()
    {
        playerX = 5.5; playerY = 4.5; heading = 0;
        enemyX = 7.5; enemyY = 4.5;
        health = 100; kills = 0; enemyAlive = true;
        respawnFrames = damageCooldown = flashFrames = 0;
        held.Clear();
    }

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) { Close(); return; }
        if (e.KeyCode == Keys.R) { ResetGame(); e.SuppressKeyPress = true; return; }
        if (e.KeyCode == Keys.Space)
        {
            Shoot(); e.SuppressKeyPress = true; return;
        }
        held.Add(e.KeyCode);
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down ||
        base.IsInputKey(keyData);

    private bool Wall(double x, double y)
    {
        int tx = (int)Math.Floor(x), ty = (int)Math.Floor(y);
        return ty < 0 || ty >= maze.Length || tx < 0 ||
            tx >= maze[ty].Length || maze[ty][tx] == '#';
    }

    private bool CanMove(double x, double y) =>
        !Wall(x - .17, y - .17) && !Wall(x + .17, y - .17) &&
        !Wall(x - .17, y + .17) && !Wall(x + .17, y + .17);

    private void UpdateGame()
    {
        if (health <= 0) return;
        if (held.Contains(Keys.Left) || held.Contains(Keys.A)) heading -= .065;
        if (held.Contains(Keys.Right) || held.Contains(Keys.D)) heading += .065;
        double move = 0;
        if (held.Contains(Keys.W) || held.Contains(Keys.Up)) move += .105;
        if (held.Contains(Keys.S) || held.Contains(Keys.Down)) move -= .105;
        double nx = playerX + Math.Cos(heading) * move;
        double ny = playerY + Math.Sin(heading) * move;
        if (CanMove(nx, playerY)) playerX = nx;
        if (CanMove(playerX, ny)) playerY = ny;
        if (flashFrames > 0) flashFrames--;
        if (damageCooldown > 0) damageCooldown--;
        if (!enemyAlive && --respawnFrames <= 0)
        {
            enemyAlive = true;
            // Two locations keep combat replayable while staying within the map.
            enemyX = kills % 2 == 0 ? 7.5 : 5.5;
            enemyY = kills % 2 == 0 ? 4.5 : 2.5;
        }
        if (enemyAlive && damageCooldown == 0 &&
            Distance(enemyX - playerX, enemyY - playerY) < 1.35)
        {
            health = Math.Max(0, health - 15);
            damageCooldown = 25;
        }
    }

    private double WallDistance(double angle)
    {
        double dx = Math.Cos(angle), dy = Math.Sin(angle);
        for (double d = .04; d < 18; d += .035)
            if (Wall(playerX + dx * d, playerY + dy * d)) return d;
        return 18;
    }

    private static double NormalizeAngle(double radians)
    {
        while (radians > Math.PI) radians -= Math.PI * 2;
        while (radians < -Math.PI) radians += Math.PI * 2;
        return radians;
    }

    private static double Distance(double dx, double dy) =>
        Math.Sqrt(dx * dx + dy * dy);

    private void Shoot()
    {
        if (health == 0) return;
        flashFrames = 3;
        if (!enemyAlive) return;
        double dx = enemyX - playerX, dy = enemyY - playerY;
        double distance = Distance(dx, dy);
        double relative = NormalizeAngle(Math.Atan2(dy, dx) - heading);
        if (Math.Abs(relative) < .14 && distance < 9 &&
            WallDistance(Math.Atan2(dy, dx)) > distance - .25)
        {
            kills++;
            enemyAlive = false;
            respawnFrames = 42;
        }
    }

    private void DrawGame()
    {
        using var g = Graphics.FromImage(frame);
        g.Clear(Color.Black);
        g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
        g.SmoothingMode = SmoothingMode.None;
        for (int x = 0; x < OledWidth; x++)
        {
            double delta = Math.Atan((x - 63.5) / (64 / Math.Tan(FieldOfView / 2)));
            double distance = WallDistance(heading + delta) * Math.Cos(delta);
            int wallHeight = Math.Clamp((int)(51 / Math.Max(distance, .2)), 1, 64);
            int top = (OledHeight - wallHeight) / 2;
            // Sparse ordered dithering gives monochrome walls depth.
            int density = distance < 2.4 ? 1 : distance < 4.5 ? 2 : 3;
            int from = Math.Max(11, top), to = Math.Min(60, top + wallHeight);
            if (density == 1)
                g.DrawLine(Pens.White, x, from, x, to - 1);
            else
                for (int y = from; y < to; y++)
                    if ((x + y * 2) % density == 0)
                        g.FillRectangle(Brushes.White, x, y, 1, 1);
        }

        if (enemyAlive)
        {
            double dist = Distance(enemyX - playerX, enemyY - playerY);
            double rel = NormalizeAngle(Math.Atan2(enemyY - playerY,
                enemyX - playerX) - heading);
            if (Math.Abs(rel) < FieldOfView / 2 &&
                WallDistance(Math.Atan2(enemyY - playerY, enemyX - playerX)) > dist - .2)
            {
                int center = (int)(64 + Math.Tan(rel) /
                    Math.Tan(FieldOfView / 2) * 64);
                int size = Math.Clamp((int)(39 / Math.Max(.3, dist)), 5, 38);
                int left = center - size / 2, top = 31 - size / 2;
                g.FillRectangle(Brushes.Black, left - 1, top - 1, size + 2, size + 2);
                using var rim = new Pen(Color.White);
                g.DrawRectangle(rim, left, top + 3, size - 1, size - 4);
                g.FillRectangle(Brushes.White, left + size / 5, top, Math.Max(1, size / 5), 4);
                g.FillRectangle(Brushes.White, left + 3 * size / 5, top, Math.Max(1, size / 5), 4);
                g.FillRectangle(Brushes.White, left + size / 5, top + size / 3,
                    Math.Max(1, size / 6), 2);
                g.FillRectangle(Brushes.White, left + 3 * size / 5, top + size / 3,
                    Math.Max(1, size / 6), 2);
            }
        }

        // Gun silhouette and crosshair, deliberately original pixel art.
        g.FillPolygon(Brushes.White,
        [
            new Point(51, 63), new Point(55, 53), new Point(60, 49),
            new Point(68, 49), new Point(73, 53), new Point(77, 63)
        ]);
        g.FillRectangle(Brushes.Black, 60, 51, 8, 12);
        if (flashFrames > 0)
        {
            g.DrawLine(Pens.White, 64, 45, 64, 31);
            g.DrawLine(Pens.White, 58, 38, 70, 38);
        }
        else
        {
            g.DrawLine(Pens.White, 62, 31, 66, 31);
            g.DrawLine(Pens.White, 64, 29, 64, 33);
        }
        g.FillRectangle(Brushes.Black, 0, 0, 128, 11);
        using var font = new Font("Consolas", 7f, FontStyle.Bold, GraphicsUnit.Pixel);
        g.DrawString($"HP {health:000}   KILLS {kills:00}", font,
            Brushes.White, new PointF(2, 1));
        if (health == 0)
        {
            g.FillRectangle(Brushes.Black, 15, 19, 99, 25);
            g.DrawRectangle(Pens.White, 15, 19, 98, 24);
            g.DrawString("GAME OVER - R", font, Brushes.White, 21, 27);
        }
    }

    private void PaintView(object? sender, PaintEventArgs e)
    {
        // Intentionally do NOT draw the raycaster frame on the PC.
        e.Graphics.Clear(Color.FromArgb(13,18,26));
        var bounds=view.ClientRectangle;
        using var headingFont=new Font("Segoe UI Semibold",23f,FontStyle.Bold);
        using var smallFont=new Font("Segoe UI",12f);
        var accent=Color.FromArgb(106,211,244);
        var titleBox=new Rectangle(bounds.X+20,bounds.Y+bounds.Height/2-95,
            Math.Max(1,bounds.Width-40),78);
        var helpBox=new Rectangle(bounds.X+20,bounds.Y+bounds.Height/2-3,
            Math.Max(1,bounds.Width-40),108);
        TextRenderer.DrawText(e.Graphics,UiLanguage.T("DOCK EKRANINA BAK 👀"),
            headingFont,titleBox,accent,
            TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|
            TextFormatFlags.WordBreak);
        TextRenderer.DrawText(e.Graphics,
            UiLanguage.English
                ?"Gameplay is on the Nova Pro Wireless OLED only.\n"+
                 "The game starts on the dock automatically. Use W / A / S / D and SPACE.\n"+
                 "Press ESC to exit and restore the previous screen."
                :"Oyun yalnızca Nova Pro Wireless OLED ekranında görünecek.\n"+
                 "Oyun dock üzerinde otomatik açılır. W / A / S / D ve SPACE kullan.\n"+
                 "ESC ile çıkıp önceki ekrana dönebilirsin.",
            smallFont,helpBox,Color.FromArgb(190,209,224),
            TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|
            TextFormatFlags.WordBreak);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { timer.Dispose(); frame.Dispose(); }
        base.Dispose(disposing);
    }
}
