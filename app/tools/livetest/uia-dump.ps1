param([int]$MaxDepth = 14)
# Prints the UI Automation tree (control type + name) of the Catogarizer test build's own windows.
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$pid2 = (Get-Process Catogarizer.App -ErrorAction Stop).Id
$walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
function Walk($el, $depth) {
  if ($depth -gt $MaxDepth) { return }
  $name = $el.Current.Name
  $type = $el.Current.ControlType.ProgrammaticName -replace 'ControlType\.', ''
  $enabled = if ($el.Current.IsEnabled) { "" } else { " [disabled]" }
  if ($name -or $type -in 'Window', 'Button', 'Edit', 'CheckBox', 'RadioButton') { ("  " * $depth) + "$type`: $name$enabled" }
  $child = $walker.GetFirstChild($el)
  while ($child) { Walk $child ($depth + 1); $child = $walker.GetNextSibling($child) }
}
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $pid2)
foreach ($w in [System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)) { Walk $w 0 }
