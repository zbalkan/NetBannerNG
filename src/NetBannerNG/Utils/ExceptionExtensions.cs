using NetBannerNG.Common.Extensions;

namespace NetBannerNG.Utils
{
    internal static class ExceptionExtensions
    {
        internal static async void Submit(this Exception ex)
        {
            try
            {
                if (App.Client is null)
                {
                    return;
                }
                await App.Client.SendException(ex.GetMessageStack()).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // IGNORE: The named pipe client may already be closed.
            }
        }
    }
}