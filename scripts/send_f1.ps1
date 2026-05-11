# Polls for the FFT window for up to N seconds, then SendKeys F1 to open the
# in-game FFTColorCustomizer config UI. Invoked by fft-dev.sh's restart helper.

param([int]$TimeoutSeconds = 30)

Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class W {
  [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern IntPtr FindWindow(string c, string n);
  [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
  public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int n);
}
"@

$titles = @(
  "FINAL FANTASY TACTICS - The Ivalice Chronicles",
  "FINAL FANTASY TACTICS"
)

for ($i = 0; $i -lt $TimeoutSeconds; $i++) {
  foreach ($t in $titles) {
    $h = [W]::FindWindow($null, $t)
    if ($h -ne [IntPtr]::Zero) {
      [W]::ShowWindow($h, 9) | Out-Null
      [W]::SetForegroundWindow($h) | Out-Null
      Start-Sleep -Milliseconds 500
      [System.Windows.Forms.SendKeys]::SendWait("{F1}")
      Write-Host "[send_f1] F1 sent to '$t'."
      exit 0
    }
  }
  Start-Sleep -Seconds 1
}
Write-Host "[send_f1] FFT window not found after $TimeoutSeconds s."
exit 1
