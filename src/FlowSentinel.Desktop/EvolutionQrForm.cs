using FlowSentinel.Application;
using FlowSentinel.Domain;

namespace FlowSentinel.Desktop;

internal sealed class EvolutionQrForm : Form
{
    private readonly IEvolutionInstanceService _service;
    private readonly ChannelConfiguration _configuration;
    private readonly Label _status = new();
    private readonly System.Windows.Forms.Timer _timer;
    private bool _checking;

    public EvolutionQrForm(
        EvolutionQrCodeResult result,
        IEvolutionInstanceService service,
        ChannelConfiguration configuration)
    {
        _service = service;
        _configuration = configuration;

        Text = "Conectar Evolution API";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(620, 760);

        _status.Text = "Aguardando leitura do QR Code...";
        _status.Dock = DockStyle.Top;
        _status.Height = 36;
        _status.Padding = new Padding(8, 10, 0, 0);
        _status.Font = new Font(Font, FontStyle.Bold);

        var picture = new PictureBox
        {
            Dock = DockStyle.Top,
            Height = 460,
            SizeMode = PictureBoxSizeMode.Zoom,
            BorderStyle = BorderStyle.FixedSingle,
            Image = TryDecode(result.Base64Image)
        };

        var details = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            Text = $"Código de pareamento: {result.PairingCode ?? "-"}\r\n\r\n{result.RawResponse}"
        };
        var close = new Button { Text = "Fechar", Dock = DockStyle.Bottom, Height = 40, DialogResult = DialogResult.OK };
        Controls.Add(details);
        Controls.Add(picture);
        Controls.Add(_status);
        Controls.Add(close);

        _timer = new System.Windows.Forms.Timer { Interval = 3000 };
        _timer.Tick += async (_, _) => await CheckStatusAsync();
        _timer.Start();
        FormClosed += (_, _) => _timer.Dispose();
    }

    private async Task CheckStatusAsync()
    {
        if (_checking)
        {
            return;
        }

        _checking = true;
        try
        {
            var result = await _service.GetStatusAsync(_configuration, CancellationToken.None);
            _status.Text = $"Estado: {result.State}";
            _status.ForeColor = result.Connected ? Color.DarkGreen : SystemColors.ControlText;
            if (result.Connected)
            {
                _timer.Stop();
                _status.Text = "Instância conectada. A configuração já pode ser utilizada.";
                MessageBox.Show(
                    this,
                    "A instância foi conectada com sucesso.",
                    "Evolution API",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }
        catch (Exception exception)
        {
            _status.Text = $"Falha ao consultar estado: {exception.Message}";
            _status.ForeColor = Color.DarkRed;
        }
        finally
        {
            _checking = false;
        }
    }

    private static Image? TryDecode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        try
        {
            var comma = value.IndexOf(',');
            var base64 = comma >= 0 ? value[(comma + 1)..] : value;
            var bytes = Convert.FromBase64String(base64);
            using var stream = new MemoryStream(bytes);
            using var image = Image.FromStream(stream);
            return new Bitmap(image);
        }
        catch
        {
            return null;
        }
    }
}
