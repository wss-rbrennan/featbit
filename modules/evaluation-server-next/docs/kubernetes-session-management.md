# Kubernetes & Container Session Management

## Problem Statement

In containerized environments (Kubernetes, Docker, Linux servers with systemd services), you may have multiple identical applications running with the same project and environment keys. This creates unique challenges for session management:

### Scenarios
1. **Kubernetes Deployment**: Multiple pods running the same application
2. **Linux Server**: Multiple systemd services using identical configurations
3. **Docker Swarm**: Multiple container instances with shared credentials

### The Challenge
```
┌─────────────────────────────────────┐
│ Kubernetes Cluster / Linux Server   │
├─────────────────────────────────────┤
│ App Instance 1: Same IP + Host      │  ← Same session ID
│ App Instance 2: Same IP + Host      │  ← Same session ID  
│ App Instance 3: Same IP + Host      │  ← Same session ID
│                                     │
│ All using:                          │
│ • Same Project Key                  │
│ • Same Environment Key              │
│ • Same Network Context             │
└─────────────────────────────────────┘
```

**Result**: Session conflicts and unpredictable behavior.

## ✅ Solution: Enhanced Multi-Layer Identification

Our enhanced `LegacyClientIdentifierGenerator` provides **three layers of identification**:

### 1. Network-Based Identification
- **IP Address**: Differentiates instances on different machines
- **Host**: Provides additional network-level differentiation

### 2. Custom Query Parameters
Applications can include custom identifiers in their WebSocket connection:

#### Standard Parameters
- `instanceId`: Unique instance identifier
- `serverId`: Server-specific identifier  
- `nodeId`: Cluster node identifier
- `region`: Geographic region
- `datacenter`: Data center location
- `version`: Application version

#### Kubernetes-Specific Parameters
- `podName`: Kubernetes pod name (unique)
- `podNamespace`: Kubernetes namespace
- `containerName`: Container name
- `replicaSet`: ReplicaSet name
- `deployment`: Deployment name

### 3. Connection-Specific Fallback (Automatic)
When network context and custom parameters are identical, the system automatically generates a unique identifier based on:
- Connection timestamp
- Token hash
- Format: `{timestamp}-{hash}` (e.g., `1751331391362-Zq_KEV`)

## 🔧 Implementation Examples

### Kubernetes Deployment
Configure your application to include pod information:

```yaml
# kubernetes-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: featbit-app
spec:
  replicas: 3
  template:
    spec:
      containers:
      - name: app
        image: my-app:latest
        env:
        - name: POD_NAME
          valueFrom:
            fieldRef:
              fieldPath: metadata.name
        - name: POD_NAMESPACE
          valueFrom:
            fieldRef:
              fieldPath: metadata.namespace
        - name: DEPLOYMENT_NAME
          value: "featbit-app"
        - name: FEATBIT_WEBSOCKET_URL
          value: "wss://featbit.example.com/hub?podName=$(POD_NAME)&podNamespace=$(POD_NAMESPACE)&deployment=$(DEPLOYMENT_NAME)"
```

### Server SDK Configuration
```javascript
// Node.js Server SDK
const client = new FeatBitClient({
  envSecret: 'your-env-secret',
  websocketUrl: 'wss://featbit.example.com/hub?instanceId=api-server-1&region=us-east-1&datacenter=dc1'
});
```

```csharp
// .NET Server SDK  
var client = new FeatBitClient(new FeatBitOptions
{
    EnvSecret = "your-env-secret",
    StreamUri = "wss://featbit.example.com/hub?instanceId=api-server-1&region=us-east-1&datacenter=dc1"
});
```

### Relay Proxy Configuration
```yaml
# docker-compose.yml
version: '3'
services:
  relay-proxy-1:
    image: featbit/relay-proxy
    environment:
      - FEATBIT_WEBSOCKET_URL=wss://featbit.example.com/hub?instanceId=relay-proxy-1&datacenter=us-east
  
  relay-proxy-2:
    image: featbit/relay-proxy
    environment:
      - FEATBIT_WEBSOCKET_URL=wss://featbit.example.com/hub?instanceId=relay-proxy-2&datacenter=us-west
```

### Linux Systemd Services
```bash
# /etc/systemd/system/featbit-app-1.service
[Unit]
Description=FeatBit Application Instance 1

[Service]
Environment=FEATBIT_WEBSOCKET_URL=wss://featbit.example.com/hub?instanceId=app-service-1&nodeId=server-01
ExecStart=/usr/local/bin/my-app
Restart=always

[Install]
WantedBy=multi-user.target
```

## 🔄 Session ID Generation Examples

### Without Custom Parameters (Uses Fallback)
```
Input:  Same IP, Same Host, Same Credentials
Output: legacy:server:abc123:env:def456  # Different due to connection-specific fallback
        legacy:server:xyz789:env:def456  # Different hash based on connection time/token
```

### With Kubernetes Parameters
```
Input:  podName=api-server-pod-abc123, podNamespace=production
Output: legacy:server:k8s-abc:env:def456

Input:  podName=api-server-pod-def456, podNamespace=production  
Output: legacy:server:k8s-def:env:def456  # Different pod = different session
```

### With Custom Instance IDs
```
Input:  instanceId=server-1, region=us-east-1
Output: legacy:server:srv-123:env:def456

Input:  instanceId=server-2, region=us-east-1
Output: legacy:server:srv-456:env:def456  # Different instance = different session
```

## ✅ Verification & Testing

### Test Multiple Instances
1. **Deploy multiple identical instances** with same credentials
2. **Check session uniqueness** in FeatBit dashboard/logs
3. **Verify session persistence** after reconnection

### Connection String Examples
```bash
# Test different pod names
wss://featbit.example.com/hub?podName=api-pod-1&podNamespace=prod
wss://featbit.example.com/hub?podName=api-pod-2&podNamespace=prod

# Test different instance IDs
wss://featbit.example.com/hub?instanceId=server-1&region=us-east
wss://featbit.example.com/hub?instanceId=server-2&region=us-east

# Even without custom params, connections will be unique due to fallback
wss://featbit.example.com/hub  # Gets unique connectionId automatically
```

## 🔧 Best Practices

### 1. **Always Include Instance Identifiers**
- Use `podName` in Kubernetes
- Use `instanceId` for server applications
- Include `region`/`datacenter` for geographic distribution

### 2. **Environment Variable Pattern**
```bash
FEATBIT_WEBSOCKET_URL="wss://featbit.example.com/hub?instanceId=${HOSTNAME}&region=${AWS_REGION}&version=${APP_VERSION}"
```

### 3. **Logging & Monitoring**
Enable debug logging to verify unique session IDs:
```json
{
  "level": "DEBUG",
  "message": "Generated legacy client ID for server connection",
  "clientId": "legacy:server:k8s-abc:env:def456",
  "connectionType": "server",
  "envId": "def456-789"
}
```

## 🚨 Troubleshooting

### Issue: Sessions Still Conflicting
**Symptoms**: Multiple instances showing as same session
**Solution**: Add more specific custom parameters

### Issue: Sessions Not Persisting
**Symptoms**: New session on every reconnection
**Solution**: Ensure custom parameters are consistent across restarts

### Issue: Too Many Unique Sessions
**Symptoms**: New session for every restart
**Solution**: Avoid using random/timestamp values in custom parameters

## 🎯 Migration Guide

### From Basic to Enhanced Identification
1. **Phase 1**: Deploy enhanced generator (backward compatible)
2. **Phase 2**: Add custom parameters to new deployments
3. **Phase 3**: Update existing deployments with custom parameters
4. **Phase 4**: Monitor and verify session uniqueness

### Zero-Downtime Migration
- ✅ **Backward Compatible**: Existing connections continue working
- ✅ **Gradual Rollout**: Add custom parameters incrementally  
- ✅ **Automatic Fallback**: Unique sessions even without configuration changes

This enhanced session management ensures reliable, persistent sessions across all containerized and multi-instance deployments while maintaining full backward compatibility. 