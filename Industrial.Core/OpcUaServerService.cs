using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Industrial.Core
{
    /// <summary>
    /// OPC UA Server 服务：将 SharedData 暴露为 OPC UA 节点，支持 Subscription 订阅推送。
    /// </summary>
    public class OpcUaServerService : BackgroundService
    {
        private readonly SharedData _sharedData;
        private readonly OpcUaSettings _settings;
        private readonly ILogger _log;
        private IndustrialServer? _server;

        public OpcUaServerService(
            SharedData sharedData,
            IOptionsMonitor<AppSettings> optionsMonitor)
        {
            _sharedData = sharedData;
            _settings = optionsMonitor.CurrentValue.OpcUa;
            _log = Log.ForContext("SourceContext", "OpcUaServer");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_settings.Enabled)
            {
                _log.Information("[OpcUa] Server is disabled in configuration");
                return;
            }

            try
            {
                await StartServerAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "[OpcUa] Server failed to start");
            }
        }

        private async Task StartServerAsync(CancellationToken cancellationToken)
        {
            // 创建应用程序配置
            var config = CreateConfiguration();

            // 确保证书目录存在
            EnsureCertificateDirectories();

            // 确保证书存储就绪
            EnsureApplicationCertificate(config);

            // 创建并启动服务器
            _server = new IndustrialServer(_sharedData, _settings);
            await _server.StartAsync(config);

            _log.Information("[OpcUa] Server started on port {Port}", _settings.Port);
            _log.Information("[OpcUa] Endpoint: opc.tcp://localhost:{Port}", _settings.Port);

            // 订阅数据变化
            _sharedData.DataChanged += OnDataChanged;

            // 等待停止信号
            var tcs = new TaskCompletionSource();
            cancellationToken.Register(() =>
            {
                _sharedData.DataChanged -= OnDataChanged;
                _server.StopAsync().AsTask().Wait();
                _log.Information("[OpcUa] Server stopped");
                tcs.TrySetResult();
            });

            await tcs.Task;
        }

        private void OnDataChanged(object? sender, DataChangedEventArgs e)
        {
            try
            {
                _server?.NotifyDataChanged();
            }
            catch (Exception ex)
            {
                _log.Error(ex, "[OpcUa] Error notifying data change");
            }
        }

        private ApplicationConfiguration CreateConfiguration()
        {
            var applicationUri = _settings.ApplicationUri;
            var applicationName = _settings.ApplicationName;

            // 使用绝对路径确保证书目录存在
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var certPath = Path.Combine(baseDir, "OPC UA", "Certificates");
            var trustedPath = Path.Combine(baseDir, "OPC UA", "Certificates", "Trusted");
            var issuerPath = Path.Combine(baseDir, "OPC UA", "Issuers");

            var config = new ApplicationConfiguration
            {
                ApplicationName = applicationName,
                ApplicationUri = applicationUri,
                ApplicationType = ApplicationType.Server,
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = certPath,
                        SubjectName = $"CN={applicationName}, O=ModbusOpcGateway, C=CN"
                    },
                    TrustedPeerCertificates = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = trustedPath
                    },
                    TrustedIssuerCertificates = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = issuerPath
                    },
                    AutoAcceptUntrustedCertificates = true,
                    AddAppCertToTrustedStore = true
                },
                TransportConfigurations = new TransportConfigurationCollection(),
                TransportQuotas = new TransportQuotas
                {
                    OperationTimeout = 15000,
                    MaxStringLength = 1048576,
                    MaxByteStringLength = 1048576,
                    MaxArrayLength = 65535,
                    MaxMessageSize = 4194304,
                    MaxBufferSize = 65535,
                    ChannelLifetime = 300000,
                    SecurityTokenLifetime = 3600000
                },
                ServerConfiguration = new ServerConfiguration
                {
                    ServerCapabilities = new StringCollection { "DA" },
                    MinRequestThreadCount = 2,
                    MaxRequestThreadCount = 10,
                    MaxQueuedRequestCount = 200,
                    BaseAddresses = new StringCollection
                    {
                        $"opc.tcp://localhost:{_settings.Port}"
                    },
                    SecurityPolicies = new ServerSecurityPolicyCollection
                    {
                        new ServerSecurityPolicy
                        {
                            SecurityMode = MessageSecurityMode.None,
                            SecurityPolicyUri = SecurityPolicies.None
                        }
                    },
                    UserTokenPolicies = new UserTokenPolicyCollection
                    {
                        new UserTokenPolicy(UserTokenType.Anonymous)
                    }
                }
            };

            return config;
        }

        private void EnsureCertificateDirectories()
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var certPath = Path.Combine(baseDir, "OPC UA", "Certificates");
                var trustedPath = Path.Combine(baseDir, "OPC UA", "Certificates", "Trusted");
                var issuerPath = Path.Combine(baseDir, "OPC UA", "Issuers");

                if (!Directory.Exists(certPath)) Directory.CreateDirectory(certPath);
                if (!Directory.Exists(trustedPath)) Directory.CreateDirectory(trustedPath);
                if (!Directory.Exists(issuerPath)) Directory.CreateDirectory(issuerPath);

                _log.Information("[OpcUa] Certificate directories created at {BaseDir}", baseDir);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "[OpcUa] Failed to create certificate directories");
            }
        }

        private void EnsureApplicationCertificate(ApplicationConfiguration config)
        {
            try
            {
                var certPath = config.SecurityConfiguration.ApplicationCertificate.StorePath;
                var subjectName = config.SecurityConfiguration.ApplicationCertificate.SubjectName;
                var applicationUri = config.ApplicationUri;

                // 确保证书目录存在
                if (!Directory.Exists(certPath))
                {
                    Directory.CreateDirectory(certPath);
                }

                // 检查是否已有证书
                var existingCerts = Directory.GetFiles(certPath, "*.pfx");
                if (existingCerts.Length > 0)
                {
                    _log.Information("[OpcUa] Using existing certificate from: {Path}", existingCerts[0]);
                    
                    // 加载证书到配置中
                    var cert = new X509Certificate2(existingCerts[0]);
                    config.SecurityConfiguration.ApplicationCertificate.Certificate = cert;
                    
                    return;
                }

                // 生成自签名证书
                _log.Information("[OpcUa] Generating self-signed certificate...");

                // 使用 CertificateFactory 生成证书
                ushort keySize = 2048;
                var notBefore = DateTime.UtcNow.AddDays(-1);
                var notAfter = DateTime.UtcNow.AddMonths(12);

                // Domain names 用于证书的 SAN (Subject Alternative Name)
                var domainNames = new List<string> { "localhost", Environment.MachineName };

                var certBuilder = CertificateFactory.CreateCertificate(
                    applicationUri,
                    subjectName,
                    null, // null for self-signed
                    domainNames
                );

                // 设置密钥大小和有效期
                certBuilder.SetNotBefore(notBefore);
                certBuilder.SetNotAfter(notAfter);
                certBuilder.SetRSAKeySize(keySize);

                // 生成证书
                var newCert = certBuilder.CreateForRSA();

                // 保存证书
                var certFileName = Path.Combine(certPath, $"{Guid.NewGuid()}.pfx");
                File.WriteAllBytes(certFileName, newCert.Export(X509ContentType.Pkcs12));
                
                // 加载证书到配置中
                config.SecurityConfiguration.ApplicationCertificate.Certificate = newCert;

                _log.Information("[OpcUa] Certificate generated: {Subject}", newCert.Subject);
                _log.Information("[OpcUa] Certificate saved to: {Path}", certFileName);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "[OpcUa] Certificate generation warning, continuing...");
            }
        }

        public override void Dispose()
        {
            _server?.Dispose();
            base.Dispose();
        }
    }

    /// <summary>
    /// 工业数据 OPC UA 服务器
    /// </summary>
    internal class IndustrialServer : StandardServer
    {
        private readonly SharedData _sharedData;
        private readonly OpcUaSettings _settings;
        private IndustrialNodeManager? _nodeManager;

        public IndustrialServer(SharedData sharedData, OpcUaSettings settings)
        {
            _sharedData = sharedData;
            _settings = settings;
        }

        protected override MasterNodeManager CreateMasterNodeManager(IServerInternal server, ApplicationConfiguration configuration)
        {
            _nodeManager = new IndustrialNodeManager(server, configuration, _sharedData);
            return new MasterNodeManager(server, configuration, null, new INodeManager[] { _nodeManager });
        }

        protected override ServerProperties LoadServerProperties()
        {
            return new ServerProperties
            {
                ManufacturerName = "ModbusOpcGateway",
                ProductName = "Industrial OPC UA Gateway",
                ProductUri = "http://modbusopcgateway.org",
                SoftwareVersion = "1.0.0",
                BuildNumber = "1.0.0",
                BuildDate = DateTime.UtcNow
            };
        }

        /// <summary>
        /// 通知数据变化，触发 Subscription 更新
        /// </summary>
        public void NotifyDataChanged()
        {
            _nodeManager?.NotifyDataChanged();
        }
    }

    /// <summary>
    /// 节点管理器：定义 OPC UA 地址空间
    /// </summary>
    internal class IndustrialNodeManager : CustomNodeManager2
    {
        private readonly SharedData _sharedData;
        private readonly List<BaseDataVariableState> _variables = new();

        public IndustrialNodeManager(IServerInternal server, ApplicationConfiguration config, SharedData sharedData)
            : base(server, config, new[] { "http://modbusopcgateway.org/Industrial" })
        {
            _sharedData = sharedData;
            SystemContext.NodeIdFactory = this;
        }

        public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
        {
            lock (Lock)
            {
                base.CreateAddressSpace(externalReferences);

                // 获取 Objects 文件夹的引用列表
                IList<IReference> references = null;
                if (!externalReferences.TryGetValue(ObjectIds.ObjectsFolder, out references))
                {
                    externalReferences[ObjectIds.ObjectsFolder] = references = new List<IReference>();
                }

                // 创建 Industrial 文件夹（parent 为 null，因为是顶级文件夹）
                var industrialFolder = new FolderState(null)
                {
                    NodeId = new NodeId("Industrial", NamespaceIndex),
                    BrowseName = new QualifiedName("Industrial", NamespaceIndex),
                    DisplayName = "Industrial",
                    TypeDefinitionId = ObjectTypeIds.FolderType,
                    Description = new LocalizedText("Industrial Data"),
                    WriteMask = AttributeWriteMask.None,
                    UserWriteMask = AttributeWriteMask.None,
                    EventNotifier = EventNotifiers.None
                };

                // 添加双向引用：Industrial -> ObjectsFolder (IsInverse=true)
                industrialFolder.AddReference(ReferenceTypeIds.Organizes, true, ObjectIds.ObjectsFolder);
                // ObjectsFolder -> Industrial (IsInverse=false)
                references.Add(new NodeStateReference(ReferenceTypeIds.Organizes, false, industrialFolder.NodeId));

                // 添加到地址空间
                AddPredefinedNode(SystemContext, industrialFolder);

                // 创建变量节点
                CreateVariable(industrialFolder, "Temperature", "Temperature (°C)", DataTypeIds.Double,
                    () => (double)_sharedData.Snapshot().Temp);
                CreateVariable(industrialFolder, "Pressure", "Pressure (kPa)", DataTypeIds.Double,
                    () => (double)_sharedData.Snapshot().Press);
                CreateVariable(industrialFolder, "Status", "Device Status", DataTypeIds.Boolean,
                    () => _sharedData.Snapshot().Status);
                CreateVariable(industrialFolder, "Mode", "Simulation Mode", DataTypeIds.Int32,
                    () => (int)_sharedData.GetMode());
                CreateVariable(industrialFolder, "NoiseMultiplier", "Noise Multiplier", DataTypeIds.Float,
                    () => _sharedData.GetNoiseMultiplier());
                CreateVariable(industrialFolder, "ResponseDelayMs", "Response Delay (ms)", DataTypeIds.Int32,
                    () => _sharedData.GetResponseDelayMs());
            }
        }

        private FolderState CreateFolder(NodeState? parent, string name, string description)
        {
            var folder = new FolderState(parent)
            {
                SymbolicName = name,
                ReferenceTypeId = ReferenceTypes.Organizes,
                TypeDefinitionId = ObjectTypeIds.FolderType,
                NodeId = new NodeId(name, NamespaceIndex),
                BrowseName = name,
                DisplayName = name,
                Description = new LocalizedText(description),
                WriteMask = AttributeWriteMask.None,
                UserWriteMask = AttributeWriteMask.None,
                EventNotifier = EventNotifiers.None
            };

            if (parent != null)
            {
                parent.AddChild(folder);
            }
            
            // 添加到地址空间
            AddPredefinedNode(SystemContext, folder);

            return folder;
        }

        private BaseDataVariableState CreateVariable(NodeState parent, string name, string description, NodeId dataType, Func<object> valueGetter)
        {
            var variable = new BaseDataVariableState(parent)
            {
                SymbolicName = name,
                ReferenceTypeId = ReferenceTypes.Organizes,
                TypeDefinitionId = VariableTypeIds.BaseDataVariableType,
                NodeId = new NodeId(name, NamespaceIndex),
                BrowseName = name,
                DisplayName = name,
                Description = new LocalizedText(description),
                WriteMask = AttributeWriteMask.None,
                UserWriteMask = AttributeWriteMask.None,
                DataType = dataType,
                ValueRank = ValueRanks.Scalar,
                AccessLevel = AccessLevels.CurrentRead,
                UserAccessLevel = AccessLevels.CurrentRead,
                Historizing = false,
                Value = valueGetter(),
                Timestamp = DateTime.UtcNow
            };

            parent.AddChild(variable);
            _variables.Add(variable);
            
            // 添加到地址空间
            AddPredefinedNode(SystemContext, variable);

            return variable;
        }

        /// <summary>
        /// 通知数据变化，触发 Subscription 推送
        /// </summary>
        public void NotifyDataChanged()
        {
            lock (Lock)
            {
                foreach (var variable in _variables)
                {
                    try
                    {
                        // 更新值并触发变化通知
                        var (temp, press, status) = _sharedData.Snapshot();

                        variable.Value = variable.SymbolicName switch
                        {
                            "Temperature" => (double)temp,
                            "Pressure" => (double)press,
                            "Status" => status,
                            "Mode" => (int)_sharedData.GetMode(),
                            "NoiseMultiplier" => _sharedData.GetNoiseMultiplier(),
                            "ResponseDelayMs" => _sharedData.GetResponseDelayMs(),
                            _ => variable.Value
                        };

                        variable.Timestamp = DateTime.UtcNow;
                        variable.StatusCode = StatusCodes.Good;
                        variable.ClearChangeMasks(SystemContext, false);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[OpcUa] Error updating variable: {ex.Message}");
                    }
                }
            }
        }
    }
}
