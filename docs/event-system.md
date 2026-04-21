# 事件架构

## 设计目的
后台线程（任务执行）与主线程（编辑器 UI）的通信机制。
- 子线程不能直接操作 Unity 对象（报错）
- 子线程通过事件发布进度，主线程处理

## EventCenter 单例

### 事件注册
```csharp
EventCenter.Instance.Register(FEventType.ParseTask, handler);
```

**线程安全**：
- 使用 `_listenersLock` 保护监听者字典修改
- 支持多个监听者订阅同一事件

### 事件发布
```csharp
EventCenter.Instance.Publish(FEventType.ParseTask, eventData);
```

**队列机制**：
- 子线程发布事件放入 `_eventQueue`（ConcurrentQueue）
- 主线程从队列取事件，同步执行监听者回调
- 避免死锁，保证主线程响应性

### 事件取消订阅
```csharp
EventCenter.Instance.UnRegister(FEventType.ParseTask, handler);
```

## 事件类型（FEventType）
- **ParseTask** - 解析进度更新
- **RefreshTask** - 刷新任务事件
- 其他任务相关事件

## 事件数据（BaseEventData）
```csharp
public class TaskProgressUpdateEvent : BaseEventData
{
    public float OldProgress { get; set; }
    public float NewProgress { get; set; }
}
```

## 完整流程示例

1. **UI 触发刷新**：用户点击"刷新缓存"按钮
2. **主线程创建任务**：启动 ParseReferenceTask
3. **子线程工作**：
   - 循环处理文件
   - 每 N 个文件更新一次进度
   - 调用 `EventCenter.Instance.Publish()`
4. **事件入队**：进度更新事件放入队列
5. **主线程处理**：每帧从队列取事件
6. **更新 UI**：显示进度条
7. **任务完成**：发布完成事件，更新数据库

## 优势
- 子线程不阻塞，持续工作
- 主线程响应灵敏，可随时取消任务
- 用户可见进度反馈
- 线程间通信解耦
