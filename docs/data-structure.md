# 数据结构设计

## FindReferenceData
单个资源的引用信息记录：
```csharp
public class FindReferenceData
{
    public string Guid { get; set; }           // 资源GUID（主键）
    public string FilePath { get; set; }       // 文件路径
    public List<string> References { get; set; }  // 该资源引用的GUID列表
}
```

## FindReferenceDataBase
ScriptableSingleton，持久化存储数据库。

### 存储结构
- **dataList** - 所有 FindReferenceData 对象列表
- **mtimeFiles / mtimeTicks** - 文件修改时间缓存（避免重复解析）

### 核心数据结构
- **_referenceDict** - ConcurrentDictionary<guid, FindReferenceData> 快速查询
- **双向引用关系** - 构建完整的引用图
  - 正向：A 引用了 B
  - 反向：B 被 A 引用

### 关键方法
- `SetData()` - 更新数据库，重建引用关系
- `UpdateChildRelation()` - 构建子节点（被引用者）的反向链接
- `QueryParents()` - 查询引用者列表
- `QueryDependencies()` - 查询依赖列表

## 并发设计
- 使用 ConcurrentDictionary 支持多线程读写
- 构建阶段使用锁保护共享状态
- mtime 缓存使用 ConcurrentDictionary

## 缓存策略
**mtime 缓存** - 存储文件最后修改时间（ticks），用于增量更新
- 未修改文件：直接使用旧数据，无需重新解析
- 修改文件：重新解析并更新 mtime
- 加速刷新流程 50% 以上

## 序列化
数据保存到 `Library/FindReference/FindReferenceDataBase.asset`
- Unity 的 ScriptableSingleton 自动序列化
- Editor 启动时自动加载
