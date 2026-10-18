$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$assets=Join-Path $root 'src/RagnarokCompanion/Assets'
Copy-Item -LiteralPath (Join-Path $root 'logo/Logo_OK_COMP.png') -Destination (Join-Path $assets 'FarmlandHorizontal.png')
Copy-Item -LiteralPath (Join-Path $root 'logo/LOGO_COMP.png') -Destination (Join-Path $assets 'FarmlandMark.png')
$source=[Drawing.Image]::FromFile((Join-Path $assets 'FarmlandMark.png'))
$frames=[Collections.Generic.List[byte[]]]::new();$sizes=@(16,24,32,48,64,128,256)
try {
 foreach($size in $sizes){
  $bitmap=[Drawing.Bitmap]::new($size,$size,[Drawing.Imaging.PixelFormat]::Format32bppArgb);$graphics=[Drawing.Graphics]::FromImage($bitmap)
  try{$graphics.Clear([Drawing.Color]::Transparent);$graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic;$graphics.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::HighQuality;$scale=[Math]::Min($size/$source.Width,$size/$source.Height);$width=[int][Math]::Round($source.Width*$scale);$height=[int][Math]::Round($source.Height*$scale);$graphics.DrawImage($source,[Drawing.Rectangle]::new([int](($size-$width)/2),[int](($size-$height)/2),$width,$height));$stream=[IO.MemoryStream]::new();try{$bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png);$frames.Add($stream.ToArray())}finally{$stream.Dispose()}}finally{$graphics.Dispose();$bitmap.Dispose()}
 }
 $file=[IO.File]::Create((Join-Path $assets 'AppIcon.ico'));$writer=[IO.BinaryWriter]::new($file)
 try{$writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$frames.Count);$offset=6+16*$frames.Count;for($i=0;$i -lt $frames.Count;$i++){$side=if($sizes[$i] -eq 256){0}else{$sizes[$i]};$writer.Write([byte]$side);$writer.Write([byte]$side);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$frames[$i].Length);$writer.Write([uint32]$offset);$offset+=$frames[$i].Length};foreach($frame in $frames){$writer.Write($frame)}}finally{$writer.Dispose();$file.Dispose()}
}finally{$source.Dispose()}
Write-Output 'Official logos copied without changes; proportional ICO frames: 16,24,32,48,64,128,256.'
