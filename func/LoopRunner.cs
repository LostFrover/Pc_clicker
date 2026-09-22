using System;
using System.Threading;

namespace Pc_clicker.func
{
    /// <summary>
    /// 循环模式
    /// </summary>
    public enum LoopMode
    {
        Count,    // 次数循环
        Timed,    // 定时循环（分钟）
        Infinite  // 无限循环
    }

    /// <summary>
    /// 在后台线程执行脚本循环，支持随时停止。
    /// 单点循环与预编辑脚本循环共用这一套执行逻辑。
    /// </summary>
    public class LoopRunner
    {
        private CancellationTokenSource _cts;
        private Thread _worker;
        private volatile bool _running;

        /// <summary>是否正在执行</summary>
        public bool IsRunning
        {
            get { return _running; }
        }

        /// <summary>执行过程中出错（在后台线程触发）</summary>
        public event Action<string> Failed;

        /// <summary>循环结束，无论是正常结束还是被手动停止（在后台线程触发）</summary>
        public event Action Finished;

        /// <summary>
        /// 开始循环执行。
        /// </summary>
        /// <param name="script">已解析好的脚本</param>
        /// <param name="target">目标（窗口或屏幕）；单点循环传 null，表示在当前鼠标位置点击</param>
        /// <param name="mode">循环模式</param>
        /// <param name="value">次数循环为次数，定时循环为间隔分钟数</param>
        public void Start(ScriptEngine script, TargetItem target, LoopMode mode, int value)
        {
            if (script == null) throw new ArgumentNullException("script");
            if (_running) return;

            _cts = new CancellationTokenSource();
            _running = true;

            CancellationToken token = _cts.Token;
            _worker = new Thread(delegate() { RunLoop(script, target, mode, value, token); });
            _worker.IsBackground = true;
            _worker.Name = "Pc_clicker.LoopRunner";
            _worker.Start();
        }

        /// <summary>请求停止（不会阻塞等待线程结束）</summary>
        public void Stop()
        {
            CancellationTokenSource cts = _cts;
            if (cts == null) return;
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void RunLoop(ScriptEngine script, TargetItem target, LoopMode mode, int value, CancellationToken token)
        {
            string error = null;

            try
            {
                if (target != null && target.Kind == TargetKind.Window)
                    ActivateTarget(target.Handle);

                int total = mode == LoopMode.Count ? Math.Max(1, value) : int.MaxValue;
                int intervalMinutes = Math.Max(1, value);

                for (int i = 0; i < total; i++)
                {
                    token.ThrowIfCancellationRequested();

                    // 定时循环：每隔指定时间执行一次
                    if (mode == LoopMode.Timed && i > 0)
                        Sleep(TimeSpan.FromMinutes(intervalMinutes), token);

                    ExecuteOnce(script, target, token);
                }
            }
            catch (OperationCanceledException)
            {
                // 用户主动停止，正常结束
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
            finally
            {
                _running = false;

                CancellationTokenSource cts = _cts;
                _cts = null;
                if (cts != null)
                {
                    try { cts.Dispose(); }
                    catch { }
                }

                if (error != null)
                    Raise(Failed, error);
                RaiseFinished();
            }
        }

        /// <summary>
        /// 完整执行一遍脚本中的全部操作。
        /// </summary>
        public static void ExecuteOnce(ScriptEngine script, TargetItem target, CancellationToken token)
        {
            foreach (ScriptAction action in script.Actions)
            {
                token.ThrowIfCancellationRequested();

                switch (action.Type)
                {
                    case "mouse":
                        MouseClick(action, target);
                        break;

                    case "key":
                        InputSimulator.KeyPress(action.Keys);
                        break;

                    case "wait":
                        Sleep(TimeSpan.FromMilliseconds(action.DelayMs), token);
                        break;
                }
            }
        }

        private static void MouseClick(ScriptAction action, TargetItem target)
        {
            int x = action.X;
            int y = action.Y;

            // (-1 -1) 表示鼠标当前位置，不做坐标换算、不移动鼠标
            if (x != -1 || y != -1)
            {
                if (target == null)
                    throw new InvalidOperationException("未选择目标，无法使用相对坐标点击。");

                if (target.Kind == TargetKind.Screen)
                {
                    x = target.ScreenLeft + action.X;
                    y = target.ScreenTop + action.Y;
                }
                else
                {
                    if (!NativeMethods.IsWindow(target.Handle))
                        throw new InvalidOperationException("目标窗口已关闭，已停止执行。");

                    var origin = new NativeMethods.POINT { X = 0, Y = 0 };
                    if (!NativeMethods.ClientToScreen(target.Handle, ref origin))
                        throw new InvalidOperationException("无法获取目标窗口的位置，已停止执行。");

                    x = origin.X + action.X;
                    y = origin.Y + action.Y;
                }
            }

            InputSimulator.MouseClick(action.Button, x, y);
        }

        /// <summary>
        /// 把目标窗口带到前台，保证点击落在目标窗口上。
        /// </summary>
        public static void ActivateTarget(IntPtr hWnd)
        {
            if (!NativeMethods.IsWindow(hWnd)) return;
            if (NativeMethods.IsIconic(hWnd))
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
            NativeMethods.SetForegroundWindow(hWnd);
        }

        /// <summary>
        /// 可被打断的等待。
        /// </summary>
        public static void Sleep(TimeSpan duration, CancellationToken token)
        {
            DateTime deadline = DateTime.UtcNow + duration;

            while (true)
            {
                token.ThrowIfCancellationRequested();

                TimeSpan remain = deadline - DateTime.UtcNow;
                if (remain <= TimeSpan.Zero) return;

                int chunk = (int)Math.Min(50, Math.Ceiling(remain.TotalMilliseconds));
                if (chunk < 1) chunk = 1;

                if (token.WaitHandle.WaitOne(chunk))
                    token.ThrowIfCancellationRequested();
            }
        }

        private static void Raise(Action<string> handler, string message)
        {
            if (handler == null) return;
            try
            {
                handler(message);
            }
            catch
            {
                // 界面回调异常不影响后台线程收尾
            }
        }

        private void RaiseFinished()
        {
            Action handler = Finished;
            if (handler == null) return;
            try
            {
                handler();
            }
            catch
            {
            }
        }
    }
}
