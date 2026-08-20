<#
.SYNOPSIS
  Turn a screenshot into the 256x256 icon.png Thunderstore requires.

.DESCRIPTION
  Centre-crops the source to a square, then scales it down with high-quality
  resampling. Thunderstore rejects anything that is not exactly 256x256, and a
  naive stretch of a 16:9 screenshot looks squashed in a way that is obvious in
  a grid of other mods.

  Cropping from the centre rather than the top: an in-game shot usually has its
  subject roughly centred and its sky and ground doing nothing, which is exactly
  what a square crop should discard.

.PARAMETER Source
  The screenshot to crop. PNG or JPG.

.PARAMETER OffsetY
  Shifts the crop window up (negative) or down (positive) in source pixels, for
  when the subject does not sit on the centre line.

.PARAMETER Output
  Where to write. Defaults to icon.png at the repo root, which is where the
  packaging script looks.

.EXAMPLE
  ./tools/Make-Icon.ps1 -Source ~/Pictures/portal.png

.EXAMPLE
  ./tools/Make-Icon.ps1 -Source shot.png -OffsetY -40
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Source,

    [int]$OffsetY = 0,

    [string]$Output
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

if (-not (Test-Path $Source)) { throw "No image at '$Source'." }
if (-not $Output) { $Output = Join-Path (Split-Path -Parent $PSScriptRoot) "icon.png" }

$image = [System.Drawing.Image]::FromFile((Resolve-Path $Source))

try {
    Write-Host "Source is $($image.Width)x$($image.Height)"

    # The largest square the image can give us, centred, then nudged by OffsetY.
    $side = [Math]::Min($image.Width, $image.Height)
    $x = [int](($image.Width - $side) / 2)
    $y = [int](($image.Height - $side) / 2) + $OffsetY

    # Clamped so an offset can never ask for pixels outside the image.
    $y = [Math]::Max(0, [Math]::Min($y, $image.Height - $side))

    Write-Host "Cropping ${side}x${side} from ($x, $y)"

    $icon = New-Object System.Drawing.Bitmap 256, 256
    $graphics = [System.Drawing.Graphics]::FromImage($icon)

    try {
        # Downscaling a screenshot by 4x or more turns thin bright details - which
        # is precisely what glowing runes are - into aliased noise without this.
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality

        $destination = New-Object System.Drawing.Rectangle 0, 0, 256, 256
        $sourceRect = New-Object System.Drawing.Rectangle $x, $y, $side, $side
        $graphics.DrawImage($image, $destination, $sourceRect, [System.Drawing.GraphicsUnit]::Pixel)
    }
    finally {
        $graphics.Dispose()
    }

    $icon.Save($Output, [System.Drawing.Imaging.ImageFormat]::Png)
    $icon.Dispose()

    Write-Host "Wrote $Output (256x256)"
}
finally {
    $image.Dispose()
}
