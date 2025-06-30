# FeatBit Evaluation Server Monitoring and Alerting Guide

This document provides recommendations for monitoring and alerting on the FeatBit Evaluation Server using built-in .NET runtime metrics collected through OpenTelemetry.

## Overview

The evaluation server automatically exposes comprehensive runtime metrics through OpenTelemetry that provide deep insights into:
- Application performance and resource usage
- .NET runtime behavior (GC, JIT, thread pool)
- Custom application metrics (WebSocket connections, message processing)
- Error rates and exceptions

These metrics are exported to your monitoring stack (Prometheus, Grafana, etc.) via the configured OpenTelemetry pipeline.

## Key Metrics for Dashboards

### 1. Application Health & Performance

#### **Response Time & Throughput**
- `http_request_duration_seconds` - HTTP request latencies
- `websocket_connections_active` - Active WebSocket connections
- `websocket_messages_processed_total` - Message throughput

**Dashboard Usage:** Create p50, p95, p99 latency graphs and connection/throughput rate graphs.

#### **Error Rates**
- `dotnet_exceptions_total` - Exception count by type
- `websocket_connections_errors_total` - WebSocket connection errors
- `websocket_connections_rejections_total` - Connection rejections

**Dashboard Usage:** Error rate percentage graphs, top exception types.

### 2. .NET Runtime Performance

#### **Memory Management**
- `dotnet_process_memory_working_set_bytes` - Physical memory usage
- `dotnet_gc_heap_total_allocated_bytes` - Total allocated memory
- `dotnet_gc_last_collection_heap_size_bytes` - Heap size by generation
- `dotnet_gc_last_collection_heap_fragmentation_size_bytes` - Heap fragmentation

**Dashboard Usage:** Memory usage trends, heap size distribution, fragmentation tracking.

#### **Garbage Collection**
- `dotnet_gc_collections_total` - GC frequency by generation
- `dotnet_gc_pause_time_seconds` - Time spent in GC pauses
- `dotnet_gc_last_collection_memory_committed_size_bytes` - Committed memory

**Dashboard Usage:** GC frequency/timing graphs, pause time impact analysis.

#### **Just-In-Time Compilation**
- `dotnet_jit_compiled_methods_total` - Methods compiled
- `dotnet_jit_compiled_il_size_bytes` - IL bytes compiled  
- `dotnet_jit_compilation_time_seconds` - Time spent in JIT

**Dashboard Usage:** JIT activity during startup, ongoing compilation overhead.

#### **Thread Pool**
- `dotnet_thread_pool_thread_count` - Active thread pool threads
- `dotnet_thread_pool_queue_length` - Queued work items
- `dotnet_thread_pool_work_item_count_total` - Completed work items

**Dashboard Usage:** Thread pool utilization, queue depth, throughput.

### 3. Application-Specific Metrics

#### **WebSocket Performance**
- `websocket_connections_duration_seconds` - Connection duration histogram
- `websocket_messages_size_bytes` - Message size distribution
- `websocket_messages_duration_seconds` - Message processing time

**Dashboard Usage:** Connection lifecycle analysis, message processing performance.

#### **System Resource Usage**
- `dotnet_process_cpu_time_seconds` - CPU usage by mode (user/system)
- `dotnet_monitor_lock_contentions_total` - Lock contention events
- `dotnet_timer_count` - Active timer instances
- `dotnet_assembly_count` - Loaded assemblies

**Dashboard Usage:** CPU utilization breakdown, contention analysis, resource tracking.

## Critical Alerts

### 1. Application Availability

#### **High Error Rate**
```yaml
alert: HighErrorRate
expr: rate(dotnet_exceptions_total[5m]) > 10
for: 2m
severity: critical
description: "Exception rate is {{ $value }} exceptions/sec"
```

#### **WebSocket Connection Issues**
```yaml
alert: HighWebSocketErrors
expr: rate(websocket_connections_errors_total[5m]) > 5
for: 1m
severity: warning
description: "WebSocket error rate: {{ $value }} errors/sec"
```

### 2. Performance Degradation

#### **High Response Latency**
```yaml
alert: HighLatency
expr: histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m])) > 1.0
for: 3m
severity: warning
description: "95th percentile latency is {{ $value }}s"
```

#### **Message Processing Delays**
```yaml
alert: SlowMessageProcessing
expr: histogram_quantile(0.95, rate(websocket_messages_duration_seconds_bucket[5m])) > 0.5
for: 2m
severity: warning
description: "95th percentile message processing time: {{ $value }}s"
```

### 3. Resource Exhaustion

#### **High Memory Usage**
```yaml
alert: HighMemoryUsage
expr: dotnet_process_memory_working_set_bytes > 3e9  # 3GB
for: 5m
severity: warning
description: "Memory usage: {{ $value | humanize1024 }}B"
```

#### **Excessive GC Pressure**
```yaml
alert: HighGCPressure
expr: rate(dotnet_gc_pause_time_seconds[5m]) > 0.1  # >10% time in GC
for: 3m
severity: warning
description: "GC pause time ratio: {{ $value | humanizePercentage }}"
```

#### **Thread Pool Saturation**
```yaml
alert: ThreadPoolSaturation
expr: dotnet_thread_pool_queue_length > 50
for: 2m
severity: warning
description: "Thread pool queue length: {{ $value }}"
```

#### **High Lock Contention**
```yaml
alert: HighLockContention
expr: rate(dotnet_monitor_lock_contentions_total[5m]) > 100
for: 2m
severity: warning
description: "Lock contention rate: {{ $value }} contentions/sec"
```

### 4. Capacity Planning

#### **Connection Limit Approaching**
```yaml
alert: HighConnectionCount
expr: websocket_connections_active > 800  # 80% of 1000 max
for: 5m
severity: warning
description: "Active connections: {{ $value }}"
```

#### **Memory Growth Trend**
```yaml
alert: MemoryGrowthTrend
expr: increase(dotnet_process_memory_working_set_bytes[30m]) > 500e6  # 500MB growth
for: 0m
severity: info
description: "Memory increased by {{ $value | humanize1024 }}B in 30min"
```

## Dashboard Layout Recommendations

### **Executive Dashboard**
- **Top Row:** Key metrics (connections, error rate, response time)
- **Second Row:** Resource utilization (CPU, memory, GC pressure)
- **Bottom:** Trends and capacity indicators

### **Engineering Dashboard**
- **Application Performance:** Latency histograms, throughput graphs
- **.NET Runtime:** GC metrics, JIT activity, thread pool utilization
- **Errors & Debugging:** Exception breakdown, stack trace analysis
- **Infrastructure:** Memory trends, CPU usage, lock contention

### **Operations Dashboard**
- **Health Status:** Up/down indicators, error rates
- **Capacity:** Connection counts, memory usage, queue depths
- **Performance:** Response times, message processing rates
- **Alerting:** Active alerts, recent incidents

## Monitoring Best Practices

1. **Use percentiles over averages** - Monitor p95/p99 latencies, not just averages
2. **Set appropriate alert thresholds** - Based on your SLA requirements and historical data
3. **Monitor trends** - Set up alerts for gradual degradation, not just absolute thresholds
4. **Correlate metrics** - High memory + high GC frequency often indicate allocation issues
5. **Dashboard drill-down** - Link high-level dashboards to detailed diagnostic views

## Metric Collection Configuration

Ensure your OpenTelemetry configuration includes:
```yaml
OTEL_METRICS_EXPORTER: prometheus
OTEL_DOTNET_AUTO_METRICS_INSTRUMENTATION_ENABLED: true
OTEL_DOTNET_AUTO_METRICS_NETRUNTIME_INSTRUMENTATION_ENABLED: true
```

The built-in .NET runtime metrics are automatically collected and provide comprehensive observability without additional overhead or custom monitoring code.

## Benefits of Using Built-in Metrics

By leveraging .NET's built-in runtime metrics instead of custom monitoring code, you get:

- **Zero overhead** - Metrics are collected efficiently by the runtime
- **Standards compliance** - Uses OpenTelemetry semantic conventions  
- **Comprehensive coverage** - No need to manually implement resource monitoring
- **Better accuracy** - Runtime has access to internal state that external monitoring can't reach
- **Maintenance-free** - No custom monitoring code to maintain or debug
- **Industry standard** - Compatible with all major monitoring platforms

This approach eliminates the need for custom performance monitoring services while providing superior observability into your application's behavior. 