using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BackgroundTask;
using Windows.ApplicationModel.Background;
using Windows.UI.Xaml;
using Windows.Storage;

namespace GlobeTrotter.Class
{
    class BgTask
    {
        BgTask()
        {
            UnregisterBackgroundTask();

            if (!AttachTask())
            {
                RegisterBackgroundTask();
            }
        }

        private void RegisterBackgroundTask()
        {
            var task = BackgroundTaskSample.RegisterBackgroundTask(BackgroundTaskSample.SampleBackgroundTaskEntryPoint,
                                                                   BackgroundTaskSample.SampleBackgroundTaskWithConditionName,
                                                                   new SystemTrigger(SystemTriggerType.UserAway, false), null);
            AttachProgressAndCompletedHandlers(task);
            UpdateUI();
        }

        private void UnregisterBackgroundTask()
        {
            BackgroundTaskSample.UnregisterBackgroundTasks(BackgroundTaskSample.SampleBackgroundTaskWithConditionName);
            UpdateUI();
        }

        private Boolean AttachTask()
        {
            Boolean _status = false;
            foreach (var task in BackgroundTaskRegistration.AllTasks)
            {
                if (task.Value.Name == BackgroundTaskSample.SampleBackgroundTaskWithConditionName)
                {
                    _status = true;
                    AttachProgressAndCompletedHandlers(task.Value);
                    BackgroundTaskSample.UpdateBackgroundTaskStatus(BackgroundTaskSample.SampleBackgroundTaskWithConditionName, true);
                    break;
                }
            }
            return _status;
        }

        private void AttachProgressAndCompletedHandlers(IBackgroundTaskRegistration task)
        {
            task.Progress += new BackgroundTaskProgressEventHandler(OnProgress);
            task.Completed += new BackgroundTaskCompletedEventHandler(OnCompleted);
        }

        private void OnProgress(IBackgroundTaskRegistration task, BackgroundTaskProgressEventArgs args)
        {
            var progress = "Progress: " + args.Progress + "%";
            BackgroundTaskSample.SampleBackgroundTaskWithConditionProgress = progress;
            UpdateUI();
        }

        private void OnCompleted(IBackgroundTaskRegistration task, BackgroundTaskCompletedEventArgs args)
        {
            UpdateUI();
        }

        private void UpdateUI()
        {
            //await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            //() =>
            //{
            //    ProgressUpdate(BackgroundTaskSample.GetBackgroundTaskStatus(BackgroundTaskSample.SampleBackgroundTaskWithConditionName), 0);
            //});
        }

    }
}

namespace BackgroundTask
{
    class BackgroundTaskSample
    {
        public const string SampleBackgroundTaskEntryPoint = "Tasks.SampleBackgroundTask";
        public const string SampleBackgroundTaskName = "SampleBackgroundTask";
        public static string SampleBackgroundTaskProgress = "";
        public static bool SampleBackgroundTaskRegistered = false;

        public const string SampleBackgroundTaskWithConditionName = "SampleBackgroundTaskWithCondition";
        public static string SampleBackgroundTaskWithConditionProgress = "";
        public static bool SampleBackgroundTaskWithConditionRegistered = false;

        public const string ServicingCompleteTaskEntryPoint = "Tasks.ServicingComplete";
        public const string ServicingCompleteTaskName = "ServicingCompleteTask";
        public static string ServicingCompleteTaskProgress = "";
        public static bool ServicingCompleteTaskRegistered = false;

        public const string TimeTriggeredTaskName = "TimeTriggeredTask";
        public static string TimeTriggeredTaskProgress = "";
        public static bool TimeTriggeredTaskRegistered = false;

        /// <summary>
        /// Register a background task with the specified taskEntryPoint, name, trigger,
        /// and condition (optional).
        /// </summary>
        /// <param name="taskEntryPoint">Task entry point for the background task.</param>
        /// <param name="name">A name for the background task.</param>
        /// <param name="trigger">The trigger for the background task.</param>
        /// <param name="condition">An optional conditional event that must be true for the task to fire.</param>
        public static BackgroundTaskRegistration RegisterBackgroundTask(String taskEntryPoint, String name, IBackgroundTrigger trigger, IBackgroundCondition condition)
        {
            var builder = new BackgroundTaskBuilder();

            builder.Name = name;
            builder.TaskEntryPoint = taskEntryPoint;
            builder.SetTrigger(trigger);

            if (condition != null)
            {
                builder.AddCondition(condition);

                //
                // If the condition changes while the background task is executing then it will
                // be canceled.
                //
                builder.CancelOnConditionLoss = true;
            }

            BackgroundTaskRegistration task = builder.Register();

            UpdateBackgroundTaskStatus(name, true);

            //
            // Remove previous completion status from local settings.
            //
            var settings = ApplicationData.Current.LocalSettings;
            settings.Values.Remove(name);

            return task;
        }

        /// <summary>
        /// Unregister background tasks with specified name.
        /// </summary>
        /// <param name="name">Name of the background task to unregister.</param>
        public static void UnregisterBackgroundTasks(string name)
        {
            //
            // Loop through all background tasks and unregister any with SampleBackgroundTaskName or
            // SampleBackgroundTaskWithConditionName.
            //
            foreach (var cur in BackgroundTaskRegistration.AllTasks)
            {
                if (cur.Value.Name == name)
                {
                    cur.Value.Unregister(true);
                }
            }

            UpdateBackgroundTaskStatus(name, false);
        }

        /// <summary>
        /// Store the registration status of a background task with a given name.
        /// </summary>
        /// <param name="name">Name of background task to store registration status for.</param>
        /// <param name="registered">TRUE if registered, FALSE if unregistered.</param>
        public static void UpdateBackgroundTaskStatus(String name, bool registered)
        {
            switch (name)
            {
                case SampleBackgroundTaskName:
                    SampleBackgroundTaskRegistered = registered;
                    break;
                case SampleBackgroundTaskWithConditionName:
                    SampleBackgroundTaskWithConditionRegistered = registered;
                    break;
                case ServicingCompleteTaskName:
                    ServicingCompleteTaskRegistered = registered;
                    break;
                case TimeTriggeredTaskName:
                    TimeTriggeredTaskRegistered = registered;
                    break;
            }
        }

        /// <summary>
        /// Get the registration / completion status of the background task with
        /// given name.
        /// </summary>
        /// <param name="name">Name of background task to retreive registration status.</param>
        public static String GetBackgroundTaskStatus(String name)
        {
            var registered = false;
            switch (name)
            {
                case SampleBackgroundTaskName:
                    registered = SampleBackgroundTaskRegistered;
                    break;
                case SampleBackgroundTaskWithConditionName:
                    registered = SampleBackgroundTaskWithConditionRegistered;
                    break;
                case ServicingCompleteTaskName:
                    registered = ServicingCompleteTaskRegistered;
                    break;
                case TimeTriggeredTaskName:
                    registered = TimeTriggeredTaskRegistered;
                    break;
            }

            var status = registered ? "Registered" : "Unregistered";

            var settings = ApplicationData.Current.LocalSettings;
            if (settings.Values.ContainsKey(name))
            {
                status += " - " + settings.Values[name].ToString();
            }

            return status;
        }
    }
}