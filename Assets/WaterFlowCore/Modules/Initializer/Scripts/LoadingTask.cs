using System;

namespace WaterFlow.Core
{
    public abstract class LoadingTask
    {
        public bool IsActive { get; private set; } = false;
        public bool IsFinished { get; private set; } = false;

        public CompleteStatus Status { get; private set; }

        public event Action<CompleteStatus> OnTaskCompleted;

        public void CompleteTask(CompleteStatus status)
        {
            if (IsFinished) return;

            Status = status;
            IsFinished = true;

            OnTaskCompleted?.Invoke(status);
        }

        public void Activate()
        {
            IsActive = true;

            try
            {
                OnTaskActivated();
            }
            catch
            {
                CompleteTask(CompleteStatus.Failed);
            }
        }

        public abstract void OnTaskActivated();

        public enum CompleteStatus { Skipped, Completed, Failed }
    }
}