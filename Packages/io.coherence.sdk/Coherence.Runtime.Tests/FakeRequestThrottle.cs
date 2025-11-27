// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// RequestThrottle implementation that can be used in tests to manually control
    /// when <see cref="WaitForCooldown"/> should complete successfully or be canceled.
    /// </summary>
    internal sealed class FakeRequestThrottle : RequestThrottle
    {
        private readonly TaskCompletionSource<bool> taskCompletionSource;

        public bool CompleteCooldown() => taskCompletionSource.TrySetResult(true);
        public bool CancelCooldown() => taskCompletionSource.TrySetCanceled();

        public FakeRequestThrottle(TaskCompletionSource<bool> taskCompletionSource = null) : base(TimeSpan.Zero)
            => this.taskCompletionSource = taskCompletionSource ?? new();

        public override Task WaitForCooldown(string path, string method, CancellationToken cancellationToken)
        {
            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() => CancelCooldown());
            }

            return taskCompletionSource.Task;
        }
    }
}
