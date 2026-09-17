param([string]$OutputPath = "src\TaskDock\Assets\TaskDock.ico")

Add-Type -AssemblyName System.Drawing
$size = 256
$bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::Transparent)

$path = [System.Drawing.Drawing2D.GraphicsPath]::new()
$radius = 54
$rect = [System.Drawing.Rectangle]::new(12, 12, 232, 232)
$diameter = $radius * 2
$path.AddArc($rect.Left, $rect.Top, $diameter, $diameter, 180, 90)
$path.AddArc($rect.Right - $diameter, $rect.Top, $diameter, $diameter, 270, 90)
$path.AddArc($rect.Right - $diameter, $rect.Bottom - $diameter, $diameter, $diameter, 0, 90)
$path.AddArc($rect.Left, $rect.Bottom - $diameter, $diameter, $diameter, 90, 90)
$path.CloseFigure()

$gradient = [System.Drawing.Drawing2D.LinearGradientBrush]::new($rect, [System.Drawing.Color]::FromArgb(79,107,237), [System.Drawing.Color]::FromArgb(109,131,242), 45)
$graphics.FillPath($gradient, $path)
$pen = [System.Drawing.Pen]::new([System.Drawing.Color]::White, 22)
$pen.StartCap = $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$graphics.DrawLines($pen, [System.Drawing.Point[]]@([System.Drawing.Point]::new(69,132), [System.Drawing.Point]::new(108,171), [System.Drawing.Point]::new(188,82)))

$memory = [System.IO.MemoryStream]::new()
$bitmap.Save($memory, [System.Drawing.Imaging.ImageFormat]::Png)
$png = $memory.ToArray()
$directory = Split-Path -Parent $OutputPath
if ($directory) { [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetFullPath($directory)) | Out-Null }
$stream = [System.IO.File]::Create([System.IO.Path]::GetFullPath($OutputPath))
$writer = [System.IO.BinaryWriter]::new($stream)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
$writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0)
$writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$png.Length); $writer.Write([uint32]22)
$writer.Write($png)
$writer.Dispose(); $stream.Dispose(); $memory.Dispose(); $pen.Dispose(); $gradient.Dispose(); $path.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
Write-Output ([System.IO.Path]::GetFullPath($OutputPath))
