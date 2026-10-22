namespace ClashRoyale
{
    using System;
    using System.Diagnostics;

    using SharpRaven.Data;

    public static class Logging
    {
        /// <summary>
        /// Logs the specified informative message.
        /// </summary>
        /// <param name="Type">The type.</param>
        /// <param name="Message">The message.</param>
        [Conditional("DEBUG")]
        public static void Info(Type Type, string Message)
        {
            Logging.Write("[ INFO  ] " + Type.Name.Pad() + " : " + Message);
        }

        private static void Write(string Line)
        {
            Debug.WriteLine(Line);
            Console.WriteLine(Line);
        }

        /// <summary>
        /// Logs the specified warning message.
        /// </summary>
        /// <param name="Type">The type.</param>
        /// <param name="Message">The message.</param>
        public static void Warning(Type Type, string Message)
        {
            Logging.Write("[WARNING] " + Type.Name.Pad() + " : " + Message);

            /* if (Sentry.Initialized)
            {
                var SentryEvent = new SentryEvent(Message)
                {
                    Level = ErrorLevel.Warning
                };

                SentryEvent.Tags.Add("className", Type.Name);
                SentryEvent.Tags.Add("projectName", Type.Assembly.GetName().Name);

                Sentry.Raven.CaptureAsync(SentryEvent);
            } */
        }

        /// <summary>
        /// Logs the specified error message.
        /// </summary>
        /// <param name="Type">The type.</param>
        /// <param name="Message">The message.</param>
        public static void Error(Type Type, string Message)
        {
            Logging.Write("[ ERROR ] " + Type.Name.Pad() + " : " + Message);

            if (Sentry.Initialized)
            {
                var SentryEvent = new SentryEvent(Message)
                {
                    Level = ErrorLevel.Error
                };

                SentryEvent.Tags.Add("className", Type.Name);
                SentryEvent.Tags.Add("projectName", Type.Assembly.GetName().Name);

                Sentry.Raven.CaptureAsync(SentryEvent);
            }
        }

        /// <summary>
        /// Logs the specified fatal error message.
        /// </summary>
        /// <param name="Type">The type.</param>
        /// <param name="Message">The message.</param>
        public static void Fatal(Type Type, string Message)
        {
            Logging.Write("[ FATAL ] " + Type.Name.Pad() + " : " + Message);

            if (Sentry.Initialized)
            {
                var SentryEvent = new SentryEvent(Message)
                {
                    Level = ErrorLevel.Fatal
                };

                SentryEvent.Tags.Add("className", Type.Name);
                SentryEvent.Tags.Add("projectName", Type.Assembly.GetName().Name);

                Sentry.Raven.CaptureAsync(SentryEvent);
            }
        }
    }
}