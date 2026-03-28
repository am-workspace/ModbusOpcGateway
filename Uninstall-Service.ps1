# Modbus OPC Gateway Windows 服务卸载脚本
# 以管理员身份运行 PowerShell 执行此脚本

$serviceName = "ModbusOpcGateway"

# 检查是否以管理员身份运行
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "请以管理员身份运行 PowerShell"
    exit 1
}

# 检查服务是否存在
$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if (-not $service) {
    Write-Host "服务 $serviceName 不存在"
    exit 0
}

# 停止服务
Write-Host "停止服务..."
Stop-Service -Name $serviceName -Force

# 删除服务
Write-Host "删除服务..."
sc.exe delete $serviceName | Out-Null

Write-Host "卸载完成！"
