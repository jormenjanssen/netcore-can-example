using System;
using System.Threading;
using System.Threading.Tasks;
using Mono.Unix;
using Mono.Unix.Native;

namespace Riwo.Common.Unix
{
    public class ApplicationLifeTimeFactory
    {
        public CancellationToken Token { get; }

        public ApplicationLifeTimeFactory()
        {
            var cts = new CancellationTokenSource();

            if(Environment.OSVersion.Platform == PlatformID.Unix)
                WaitForCancellationAsync(cts);

            Token = cts.Token;
        }

        private async Task WaitForCancellationAsync(CancellationTokenSource cancellationTokenSource)
        {
            await Task.Run(() => Mono.Unix.UnixSignal.WaitAny(new[] {new UnixSignal(Signum.SIGINT | Signum.SIGTERM) })).ConfigureAwait(false);
            cancellationTokenSource.Cancel();
        }
    }
}
