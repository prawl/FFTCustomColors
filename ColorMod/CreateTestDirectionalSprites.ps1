# PowerShell script to create test directional sprites with labels
# This creates visually distinct images for testing the carousel

Add-Type -AssemblyName System.Drawing

$sourceImage = "ColorMod\Resources\Previews\squire_male_original.png"
$directions = @{
    "_n" = "NORTH"
    "_ne" = "NORTH-EAST"
    "_e" = "EAST"
    "_se" = "SOUTH-EAST"
    "_s" = "SOUTH"
    "_sw" = "SOUTH-WEST"
    "_w" = "WEST"
    "_nw" = "NORTH-WEST"
}

if (Test-Path $sourceImage) {
    $img = [System.Drawing.Image]::FromFile((Resolve-Path $sourceImage))

    foreach ($suffix in $directions.Keys) {
        $label = $directions[$suffix]
        $outputPath = $sourceImage.Replace(".png", "$suffix.png")

        # Create a copy of the image
        $bitmap = New-Object System.Drawing.Bitmap($img)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

        # Add text overlay
        $font = New-Object System.Drawing.Font("Arial", 16, [System.Drawing.FontStyle]::Bold)
        $brush = [System.Drawing.Brushes]::Yellow
        $shadowBrush = [System.Drawing.Brushes]::Black

        # Measure text to center it
        $textSize = $graphics.MeasureString($label, $font)
        $x = ($bitmap.Width - $textSize.Width) / 2
        $y = 10

        # Draw shadow
        $graphics.DrawString($label, $font, $shadowBrush, $x + 2, $y + 2)
        # Draw text
        $graphics.DrawString($label, $font, $brush, $x, $y)

        # Save the modified image
        $bitmap.Save($outputPath)

        Write-Host "Created $outputPath with label: $label"

        $graphics.Dispose()
        $bitmap.Dispose()
    }

    $img.Dispose()
    Write-Host "`nTest directional sprites created successfully!"
} else {
    Write-Host "Source image not found: $sourceImage"
}