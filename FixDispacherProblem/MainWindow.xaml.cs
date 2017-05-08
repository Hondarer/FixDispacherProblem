using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace FixDispacherProblem
{
    /// <summary>
    /// <see cref="Dispatcher"/> の支援機能を提供します。
    /// </summary>
    public class DispatcherHelper
    {
        /// <summary>
        /// バックグラウンドスレッドにて、引数に与えられた <see cref="Action"/> を実行します。
        /// <see para="action"/> 内にて <see cref="Dispatcher"/> が生成された場合、処理を返す際にその <see cref="Dispatcher"/> はシャットダウンされます。
        /// </summary>
        /// <param name="action">処理したい <see cref="Action"/>。</param>
        /// <remarks>
        /// 本メソッドを経由しない方法で、バックグラウンドスレッドで <see cref="DispatcherObject"/> を生成する操作は行わないでください。
        /// バックグラウンドスレッドで <see cref="Dispatcher"/> を生成し処置を行わない場合、深刻なメモリー リークを引き起こします。
        /// </remarks>
        public static void InvokeBackground(Action action)
        {
            ThreadStart threadStart = () =>
            {
                try
                {
                    action();
                }
                finally
                {
                    Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.SystemIdle);
                    Dispatcher.Run();
                }
            };

            Thread thread = new Thread(threadStart)
            {
                Name = string.Format("DispatcherSafeAction from {0}", Thread.CurrentThread.ManagedThreadId),
                IsBackground = true
            };

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        /// <summary>
        /// バックグラウンドスレッドにて、引数に与えられた <see cref="Action"/> を実行する <see cref="Task"/> を返します。
        /// <see para="action"/> 内にて <see cref="Dispatcher"/> が生成された場合、処理を返す際にその <see cref="Dispatcher"/> はシャットダウンされます。
        /// </summary>
        /// <param name="action">処理したい <see cref="Action"/>。</param>
        /// <returns>処理したい <see cref="Action"/> の完了を待ち合わせる <see cref="Task"/>。</returns>
        /// <remarks>
        /// 本メソッドを経由しない方法で、バックグラウンドスレッドで <see cref="DispatcherObject"/> を生成する操作は行わないでください。
        /// バックグラウンドスレッドで <see cref="Dispatcher"/> を生成し処置を行わない場合、深刻なメモリー リークを引き起こします。
        /// </remarks>
        public static Task BeginInvokeBackground(Action action)
        {
            return Task.Run(() =>
            {
                InvokeBackground(action);
            });
        }
    }

    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// <see cref="MainWindow"/> の新しいインスタンスを初期化します。
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 初回の <see cref="OnActivated"/> イベントかどうかを保持します。
        /// </summary>
        private bool isFirstCall = true;

        /// <summary>
        /// <see cref="Window.Activated"/> イベントを発生させます。
        /// </summary>
        /// <param name="e">イベント データを格納している <see cref="EventArgs"/>。</param>
        protected override void OnActivated(EventArgs e)
        {
            const string path = @"C:\Windows\Web\Wallpaper\Theme1\img1.jpg";
            const BitmapCreateOptions createOption = BitmapCreateOptions.None;
            const BitmapCacheOption cacheOption = BitmapCacheOption.Default;

            base.OnActivated(e);

            if (isFirstCall == true)
            {
                isFirstCall = false;

                Task.Run(() =>
                {
                    for (long index = 0; index < long.MaxValue; index++)
                    {
                        Dispatcher.Invoke(new Action(() =>
                        {
                            txt.Text = string.Format("turn {0} Total Memory = {1} KB", index, GC.GetTotalMemory(true) / 1024); ;
                            img.Source = null;
                        }));

                        DispatcherHelper.InvokeBackground(() =>
                        {
                            BitmapDecoder decoder = BitmapDecoder.Create(new Uri(path), createOption, cacheOption);
                            BitmapSource bmp = new WriteableBitmap(decoder.Frames[0]);
                            bmp.Freeze();

                            // バックグラウンドから、UI スレッドの Dispatcher の呼び出しを待ち合わせている間に
                            // アプリケーションの終了を行おうとすると、
                            // System.Threading.Tasks.TaskCanceledException が発生する。
                            // この例外は、ユーザーコードでキャッチすべき。
                            try
                            {
                                Dispatcher.Invoke(new Action(() =>
                                {
                                    img.Source = bmp;
                                }));
                            }
                            catch (TaskCanceledException ex)
                            {
                                // 構造上、プロセス終了のタイミングでしか通過しない。
                                Debug.WriteLine(string.Format("Dispatcher への依頼待ち合わせ中に、プロセスが終了しました:\r\n{0}", ex.ToString()));
                            }
                        });

                        //DispatcherHelper.BeginInvokeBackground(() =>
                        //{
                        //    BitmapDecoder decoder = BitmapDecoder.Create(new Uri(path), createOption, cacheOption);
                        //    BitmapSource bmp = new WriteableBitmap(decoder.Frames[0]);
                        //    bmp.Freeze();

                        //    // バックグラウンドから、UI スレッドの Dispatcher の呼び出しを待ち合わせている間に
                        //    // アプリケーションの終了を行おうとすると、
                        //    // System.Threading.Tasks.TaskCanceledException が発生する。
                        //    // この例外は、ユーザーコードでキャッチすべき。
                        //    try
                        //    {
                        //        Dispatcher.Invoke(new Action(() =>
                        //        {
                        //            img.Source = bmp;
                        //        }));
                        //    }
                        //    catch (TaskCanceledException ex)
                        //    {
                        //        // 構造上、プロセス終了のタイミングでしか通過しない。
                        //        Debug.WriteLine(string.Format("Dispatcher への依頼待ち合わせ中に、プロセスが終了しました:\r\n{0}", ex.ToString()));
                        //    }
                        //}).GetAwaiter().GetResult();

                        Thread.Sleep(100);
                    }
                });
            }
        }
    }
}
