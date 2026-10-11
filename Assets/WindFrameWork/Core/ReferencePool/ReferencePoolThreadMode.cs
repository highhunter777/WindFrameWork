namespace WindFrameWork.Core.ReferencePool
{
    /// <summary>
    /// 引用池线程模型。
    /// </summary>
    public enum ReferencePoolThreadMode
    {
        /// <summary>
        /// 加锁模式（默认）：任意线程租还与查询均安全。
        /// </summary>
        /// <remarks>
        /// 引用池的元素是纯托管对象，不像 <see cref="UnityEngine.Object"/> 那样受主线程约束，
        /// 因此把「可用范围限定在主线程」换成「可用范围不加限定」才是本模块的默认形态。
        /// 加锁只在池对象上做互斥，工厂与业务回调仍在调用线程上执行。
        /// </remarks>
        Synchronized = 0,

        /// <summary>
        /// 单线程亲和：不加锁，换取更短的租还路径；开发构建下断言调用线程为创建线程。
        /// </summary>
        /// <remarks>
        /// 适用于确认只在一个线程上流转的池。跨线程使用属未定义行为：
        /// 计数与空闲链表会同时被两个线程改写，且这种损坏通常在数分钟之后才以数量对不上的形式显现。
        /// </remarks>
        SingleThread = 1
    }
}
