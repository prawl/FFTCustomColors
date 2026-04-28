Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class W {
  public delegate bool EnumProc(IntPtr h, IntPtr p);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
}
'@

# Find FFT_enhanced PID
$proc = Get-Process FFT_enhanced -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { Write-Error "FFT not running"; exit 1 }
$targetPid = [uint32]$proc.Id

# Enumerate all visible windows belonging to that PID; pick the largest one
# (game window is much bigger than the log console).
$best = $null
$cb = [W+EnumProc]{
    param($h, $p)
    if (-not [W]::IsWindowVisible($h)) { return $true }
    $wpid = 0
    [W]::GetWindowThreadProcessId($h, [ref]$wpid) | Out-Null
    if ($wpid -ne $targetPid) { return $true }
    $r = New-Object W+RECT
    if (-not [W]::GetWindowRect($h, [ref]$r)) { return $true }
    $area = ($r.R - $r.L) * ($r.B - $r.T)
    if ($best -eq $null -or $area -gt $best.Area) {
        $script:best = [PSCustomObject]@{
            Handle = $h
            L = $r.L; T = $r.T
            W = ($r.R - $r.L); H = ($r.B - $r.T)
            Area = $area
        }
    }
    return $true
}
[W]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null

if (-not $best) { Write-Error "No visible windows for FFT"; exit 1 }

$bitmap = New-Object System.Drawing.Bitmap($best.W, $best.H)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.CopyFromScreen($best.L, $best.T, 0, 0, (New-Object System.Drawing.Size($best.W, $best.H)))
$graphics.Dispose()

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$path = "$env:USERPROFILE\Downloads\fftwin_$timestamp.png"
$bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
$bitmap.Dispose()
Write-Output $path
