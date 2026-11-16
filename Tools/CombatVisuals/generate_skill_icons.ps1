# Native vector authoring, not a screenshot or crop. SVG source and transparent PNG share the same paths.
param([string]$RepoRoot = (Resolve-Path "$PSScriptRoot/../..").Path)
Add-Type -AssemblyName System.Drawing
$iconRoot = Join-Path $RepoRoot 'Capstone/Assets/Game/WorldFeedback/Resources/WorldFeedback/Icons'
New-Item -ItemType Directory -Path $iconRoot -Force | Out-Null
function New-Shape($kind, $points, $width=6, $filled=$false) { @{Kind=$kind;Points=$points;Width=$width;Filled=$filled} }
$icons = @{
    bite = @(
        (New-Shape 'poly' @(23,37,34,43,41,69,51,51,62,69,72,43,82,37) 3 $false),
        (New-Shape 'poly' @(29,37,41,42,37,60) 3 $true),
        (New-Shape 'poly' @(61,42,74,37,66,60) 3 $true),
        (New-Shape 'line' @(24,73,37,82,53,85,70,79,80,70) 5 $false)
    )
    blast = @(
        (New-Shape 'poly' @(20,47,30,42,38,28,46,33,43,54,34,66,21,64) 3 $true),
        (New-Shape 'line' @(53,35,62,42,65,51,62,60,53,67) 6 $false),
        (New-Shape 'line' @(64,24,77,36,81,51,77,68,64,80) 6 $false),
        (New-Shape 'line' @(76,16,91,34,96,51,91,74,76,90) 4 $false)
    )
    projectile = @(
        (New-Shape 'line' @(13,78,25,72,34,67) 5 $false),
        (New-Shape 'line' @(17,90,30,84,42,75) 4 $false),
        (New-Shape 'poly' @(56,25,77,45,72,66,52,79,34,61,36,40) 4 $false),
        (New-Shape 'poly' @(56,34,67,51,54,68,44,52) 2 $true),
        (New-Shape 'line' @(41,20,60,14,80,27,91,49,85,69) 4 $false),
        (New-Shape 'line' @(77,80,91,71,97,59) 3 $false)
    )
    wing_strike = @(
        (New-Shape 'poly' @(14,70,36,39,70,20,93,17,74,30,49,50,33,69) 2 $true),
        (New-Shape 'poly' @(25,86,51,56,91,36,79,54,55,71,42,87) 2 $true),
        (New-Shape 'line' @(51,91,74,80,88,66) 4 $false)
    )
}
foreach ($id in $icons.Keys) {
    $bitmap = [System.Drawing.Bitmap]::new(256,256,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.ScaleTransform(2.5,2.5)
    $svg = [System.Text.StringBuilder]::new('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 104 104"><g stroke-linecap="round" stroke-linejoin="round">')
    foreach ($shape in $icons[$id]) {
        $points = [System.Collections.Generic.List[System.Drawing.PointF]]::new()
        $pairs = [System.Collections.Generic.List[string]]::new()
        for ($i=0; $i -lt $shape.Points.Length; $i+=2) {
            $points.Add([System.Drawing.PointF]::new($shape.Points[$i],$shape.Points[$i+1]))
            $pairs.Add("$($shape.Points[$i]),$($shape.Points[$i+1])")
        }
        $color = if ($id -eq 'wing_strike') {[System.Drawing.Color]::FromArgb(246,182,236,249)} elseif ($id -eq 'projectile') {[System.Drawing.Color]::FromArgb(250,210,194,255)} else {[System.Drawing.Color]::FromArgb(250,242,229,197)}
        $pen = [System.Drawing.Pen]::new($color,[float]$shape.Width)
        $pen.StartCap=$pen.EndCap=[System.Drawing.Drawing2D.LineCap]::Round
        $pen.LineJoin=[System.Drawing.Drawing2D.LineJoin]::Round
        $brush = [System.Drawing.SolidBrush]::new($color)
        if ($shape.Filled) { $g.FillPolygon($brush,$points.ToArray()) } elseif ($shape.Kind -eq 'poly') { $g.DrawPolygon($pen,$points.ToArray()) } else { $g.DrawLines($pen,$points.ToArray()) }
        $hex = '#{0:X2}{1:X2}{2:X2}' -f $color.R,$color.G,$color.B
        $tag = if ($shape.Kind -eq 'poly') {'polygon'} else {'polyline'}
        $fill = if ($shape.Filled) {$hex} else {'none'}
        [void]$svg.Append("<$tag points=`"$($pairs -join ' ')`" stroke=`"$hex`" stroke-width=`"$($shape.Width)`" fill=`"$fill`"/>")
        $pen.Dispose(); $brush.Dispose()
    }
    [void]$svg.Append('</g></svg>')
    $bitmap.Save((Join-Path $iconRoot "Skill_$id.png"),[System.Drawing.Imaging.ImageFormat]::Png)
    $svg.ToString() | Set-Content -LiteralPath (Join-Path $iconRoot "Skill_$id.svg") -Encoding utf8
    $g.Dispose(); $bitmap.Dispose()
}
Write-Output 'Authored 4 skill SVGs and 4 transparent 256px PNGs.'
