using System;

namespace NetBannerNG.Services
{
    public enum GroupHealthState
    {
        Healthy,
        Degraded,
        Disabled
    }

    public sealed class GroupHealthPolicy
    {
        private readonly int _disableThreshold;
        private readonly TimeSpan _disableDuration;
        private int _consecutiveFailures;
        private DateTime _disabledUntilUtc = DateTime.MinValue;

        public GroupHealthPolicy(int disableThreshold, TimeSpan disableDuration)
        {
            _disableThreshold = disableThreshold >= 1 ? disableThreshold : throw new ArgumentOutOfRangeException(nameof(disableThreshold), disableThreshold, "disableThreshold must be at least 1.");
            _disableDuration = disableDuration > TimeSpan.Zero ? disableDuration : throw new ArgumentOutOfRangeException(nameof(disableDuration), disableDuration, "disableDuration must be greater than zero.");
            State = GroupHealthState.Healthy;
        }

        public GroupHealthState State { get; private set; }
        public int ConsecutiveFailures => _consecutiveFailures;

        public bool CanAttempt(DateTime utcNow)
        {
            if (State != GroupHealthState.Disabled)
            {
                return true;
            }

            if (utcNow < _disabledUntilUtc)
            {
                return false;
            }

            State = GroupHealthState.Degraded;
            _consecutiveFailures = 0;
            return true;
        }

        public void RecordSuccess()
        {
            _consecutiveFailures = 0;
            State = GroupHealthState.Healthy;
        }

        public void RecordFailure(DateTime utcNow)
        {
            _consecutiveFailures++;
            State = _consecutiveFailures >= _disableThreshold ? GroupHealthState.Disabled : GroupHealthState.Degraded;
            if (State == GroupHealthState.Disabled)
            {
                _disabledUntilUtc = utcNow.Add(_disableDuration);
            }
        }
    }
}