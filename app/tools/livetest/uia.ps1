param([string]$Action, [string]$Name)
# Minimal UI Automation driver scoped to the Catogarizer test build's own windows.
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$pid2 = (Get-Process Catogarizer.App).Id
$root = [System.Windows.Automation.AutomationElement]::RootElement
$procCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $pid2)
$windows = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $procCond)
$nameCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $Name)
foreach ($w in $windows) {
  $el = $w.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $nameCond)
  if ($null -eq $el) { continue }
  switch ($Action) {
    "invoke" { $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
    "select" { $el.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select() }
  }
  "done: $Action '$Name'"
  return
}
"not found: '$Name'"
