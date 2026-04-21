# 任务执行和并行处理

## 任务体系

### GetFilePathListTask
获取所有需要扫描的资源文件列表。

**输入**：配置的扫描路径和后缀名
**输出**：文件路径列表

**优化**：
- AssetDatabase 预取 - 批量获取资源元数据，减少 API 调用

### ParseReferenceTask
核心解析任务，提取每个文件的 GUID 引用。

**输入**：文件列表、CancellationToken、mtime 缓存、旧数据
**输出**：FindReferenceData 列表 + 新 mtime 字典

**处理流程**：
1. 检查文件 mtime，与缓存对比
2. 未修改文件：使用旧数据，跳过解析
3. 修改文件：读取内容，解析 YAML/JSON，提取 GUID
4. 并行处理，收集结果

**并行优化**：
```csharp
var parallelOptions = new ParallelOptions 
{ 
    MaxDegreeOfParallelism = Environment.ProcessorCount,
    CancellationToken = cancellationToken 
};
```

- 线程数 = CPU 核心数
- 支持任意中途取消

**线程本地 List**：
每个线程维护本地结果列表，避免 ConcurrentBag 争用，最后统一合并。

## 正则优化
将多个 GUID 查询的正则表达式合并成单次匹配，减少正则引擎开销。

## 进度更新限流
- 原始：每处理一个文件更新一次进度
- 优化：仅每处理 N 个文件更新一次（减少事件发布）
- 效果：减少 UI 卡顿

## CancellationToken
所有后台任务都主动检查 CancellationToken：
- `cancellationToken.ThrowIfCancellationRequested()`
- 支持用户取消长时间运行的任务
- 不捕获 ThreadAbortException，让线程正常终止

## 增量更新流程
1. 监听资源变化事件
2. 对改动文件进行 mtime 对比
3. 仅重新解析改动文件
4. 合并新旧数据，更新数据库
5. 发布更新事件，UI 刷新

**性能收益**：
- 小改动只需秒级更新
- 避免重新扫描整个项目
