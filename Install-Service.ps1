# Modbus OPC Gateway Windows 服务安装脚本
# 以管理员身份运行 PowerShell 执行此脚本

$serviceName = "ModbusOpcGateway"
$serviceDisplayName = "Modbus OPC Gateway"
$serviceDescription = "Modbus 从站模拟器与 OPC UA 网关服务，7×24 小时运行"
$exePath = "$PSScriptRoot\ModbusOpcGateway\bin\Release\net10.0\ModbusOpcGateway.exe"

# 检查是否以管理员身份运行
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "请以管理员身份运行 PowerShell"
    exit 1
}

# 检查可执行文件是否存在
if (-not (Test-Path $exePath)) {
    Write-Error "找不到可执行文件: $exePath"
    Write-Host "请先运行: dotnet build -c Release"
    exit 1
}

# 如果服务已存在，先停止并删除
$existingService = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "停止并删除现有服务..."
    Stop-Service -Name $serviceName -Force
    sc.exe delete $serviceName | Out-Null
    Start-Sleep -Seconds 2
}

# 创建服务
Write-Host "创建 Windows 服务..."
sc.exe create $serviceName `
    binPath= $exePath `
    start= auto `
    displayName= $serviceDisplayName `
    obj= LocalSystem | Out-Null

# 设置服务描述
sc.exe description $serviceName $serviceDescription | Out-Null

# 启动服务
Write-Host "启动服务..."
Start-Service -Name $serviceName

# 检查服务状态
$service = Get-Service -Name $serviceName
Write-Host "服务状态: $($service.Status)"
Write-Host "安装完成！"
Write-Host ""
Write-Host "常用命令:"
Write-Host "  启动服务: Start-Service -Name $serviceName"
Write-Host "  停止服务: Stop-Service -Name $serviceName"
Write-Host "  查看日志: Get-Content .\logs\modbus-simulator-*.log -Tail 50"
