# FindReference - 项目文档

**项目目标**：Unity 编辑器插件，快速查找资源引用关系。显示哪些资源直接或间接引用了选中的资源。

## 核心模块

### FindReferenceCore（引擎核心）
单例类，提供公开 API 查询引用关系。
- `QueryParents(guid)` - 查找直接引用该资源的所有资源
- `QueryDependencies(guid)` - 查找该资源依赖的资源
- 维护数据库连接和生命周期

### FindReferenceWindow（UI 窗口）
编辑器窗口实现。
- 菜单命令：`Window > FindReference` 或快捷键 `Ctrl+Shift+Alt+F`
- 展示选中资源及其引用/被引用列表
- 可点击跳转到对应资源

### 数据层
详见 [数据结构设计](./docs/data-structure.md)

## 任务流程

### 初始化流程
1. 获取所有资源文件列表 (`GetFilePathListTask`)
2. 解析每个文件，提取 GUID 引用 (`ParseReferenceTask`)
3. 构建双向引用图，缓存到本地
4. UI 可从 `FindReferenceCore` 查询

### 刷新流程
- 监听资源变化 (`FindReferenceAssetPostProcessor`)
- 仅增量更新改动文件
- 使用 mtime 缓存加速对比

详见 [任务执行和并行处理](./docs/task-execution.md)

## 事件系统
详见 [事件架构](./docs/event-system.md)

事件由子线程发布，主线程处理，避免阻塞编辑器。

## 性能优化

最近版本优化点：
- **四方向并行** - AssetDatabase 预取、正则合并、线程本地 List、mtime 缓存
- **进度更新限流** - 减少更新频率避免卡顿
- **CancellationToken** - 主动检查取消标志

详见 [性能优化方案](./docs/performance.md)

## 配置系统
`FindReferenceConfig` - 支持配置扫描后缀名、包含路径等参数

## 日志和调试
`FindReferenceLogger` - 统一日志输出，支持控制台打印指定后缀名的文件路径
