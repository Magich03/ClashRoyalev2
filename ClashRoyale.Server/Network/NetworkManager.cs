namespace ClashRoyale.Network
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using ClashRoyale.Crypto;
    using ClashRoyale.Crypto.Encrypters;
    using ClashRoyale.Crypto.Inits;
    using ClashRoyale.Enums;
    using ClashRoyale.Exceptions;
    using ClashRoyale.Extensions;
    using ClashRoyale.Handlers;
    using ClashRoyale.Logic;
    using ClashRoyale.Logic.Structures;
    using ClashRoyale.Maths;
    using ClashRoyale.Messages;

    public class NetworkManager
    {
        public Device Device;
        public RequestTime RequestTime;
        public LogicLong AccountId;

        public int Ping;
        public int InvalidMessageStateCnt;

        public string Interface;

        public DateTime LastChatMessage;
        public DateTime LastKeepAlive;
        public DateTime LastMessage;
        public DateTime Session;

        public PepperInit PepperInit;
        public IEncrypter SendEncrypter;
        public IEncrypter ReceiveEncrypter;

        public IPEndPoint UdpEndPoint;

        /// <summary>
        /// Logs raw packet data for debugging.
        /// </summary>
        private void LogPacket(string direction, short type, byte[] data)
        {
            string packetData = BitConverter.ToString(data).Replace("-", " ");
            Debug.WriteLine($"[DEBUG] {direction} Packet: Type={type}, Length={data.Length}, Data={packetData}");
        }

        /// <summary>
        /// Gets the time since last keep alive in ms.
        /// </summary>
        public long TimeSinceLastKeepAliveMs => (long)DateTime.UtcNow.Subtract(this.LastKeepAlive).TotalMilliseconds;

        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkManager"/> class.
        /// </summary>
        /// <param name="Device">The device.</param>
        public NetworkManager(Device Device)
        {
            this.Device = Device;
            this.Session = DateTime.UtcNow;
            this.LastMessage = DateTime.UtcNow;
            this.LastKeepAlive = DateTime.UtcNow;
        }

        /// <summary>
        /// Receives the message.
        /// </summary>
        public void ReceiveMessage(short Type, short Version, byte[] Packet)
        {
            LogPacket("Received", Type, Packet);

            if (this.ReceiveEncrypter == null)
            {
                if (this.PepperInit.State == 0)
                {
                    if (Type == 10101)
                    {
                        this.SendEncrypter = new Rc4Encrypter("fhsd6f86f67rt8fw78fw789we78r9789wer6re", "nonce");
                        this.ReceiveEncrypter = new Rc4Encrypter("fhsd6f86f67rt8fw78fw789we78r9789wer6re", "nonce");
                        Packet = this.ReceiveEncrypter.Decrypt(Packet);
                    }
                    else if (Type == 10100)
                    {
                        Packet = PepperCrypto.HandlePepperAuthentification(ref this.PepperInit, Packet);
                    }
                    else
                    {
                        Packet = null;
                    }
                }
                else if (this.PepperInit.State == 2)
                {
                    Packet = Type == 10101 ? PepperCrypto.HandlePepperLogin(ref this.PepperInit, Packet) : null;
                }
                else
                {
                    Packet = null;
                }
            }
            else
            {
                Packet = this.ReceiveEncrypter.Decrypt(Packet);
            }

            if (Packet != null)
            {
                using (ByteStream Stream = new ByteStream(Packet))
                {
                    Message Message = MessageFactory.CreateMessage(Type, Stream);

                    if (Message != null)
                    {
                        Logging.Info(this.GetType(), "Receiving " + Message.GetType().Name + ".");

                        if (this.RequestTime.CanHandleMessage(Message))
                        {
                            try
                            {
                                Message.Decode();
                            }
                            catch (Exception Exception)
                            {
                                Logging.Error(this.GetType(), $"ReceiveMessage() - Error processing message {Message.Type}: {Exception}");
                            }

                            HandlerFactory.MessageHandle(this.Device, Message);
                        }
                    }
                }
            }
            else
            {
                Logging.Error(this.GetType(), "Packet == null at ReceiveMessage().");
            }
        }

        /// <summary>
        /// Sends the specified message.
        /// </summary>
        /// <param name="Message">The message.</param>
        public void SendMessage(Message Message)
        {
            Logging.Info(typeof(NetworkTcp), "Sending " + Message.GetType().Name + ".");

            if (this.Device.Token.IsConnected)
            {
                if (Message.IsServerToClientMessage)
                {
                    Message.Encode();
                    byte[] Bytes = Message.Stream.ToArray(0, Message.Stream.Offset);

                    if (this.SendEncrypter == null)
                    {
                        if (this.PepperInit.State == 1)
                        {
                            Bytes = PepperCrypto.SendPepperAuthentificationResponse(ref this.PepperInit, Bytes);
                        }
                        else if (this.PepperInit.State == 3)
                        {
                            Bytes = PepperCrypto.SendPepperLoginResponse(ref this.PepperInit, out this.SendEncrypter, out this.ReceiveEncrypter, Bytes);
                        }
                    }
                    else
                    {
                        Bytes = this.SendEncrypter.Encrypt(Bytes);
                    }

                    Message.Stream.SetByteArray(Bytes);

                    LogPacket("Sent", Message.Type, Bytes);
                    NetworkTcp.Send(this.WriteHeader(Message), this.Device.Token);
                    HandlerFactory.MessageHandle(this.Device, Message);
                }
                else
                {
                    Logging.Error(this.GetType(), $"Message.IsServerToClientMessage != true at SendMessage(Message {Message.Type}).");
                }
            }
        }

        /// <summary>
        /// Writes the header of a message.
        /// </summary>
        public byte[] WriteHeader(Message Message)
        {
            byte[] Stream = Message.GetBytes();
            byte[] Packet = new byte[7 + Stream.Length];

            int MessageLength = Stream.Length;
            int MessageType = Message.Type;
            int MessageVersion = Message.Version;

            if (MessageLength > 0xFFFFFF)
            {
                throw new LogicException(this.GetType(), "Message is too big. Length: " + MessageLength);
            }

            Array.Copy(Stream, 0, Packet, 7, MessageLength);

            Packet[1] = (byte)(MessageType);
            Packet[0] = (byte)(MessageType >> 8);

            Packet[4] = (byte)(MessageLength);
            Packet[3] = (byte)(MessageLength >> 8);
            Packet[2] = (byte)(MessageLength >> 16);

            Packet[6] = (byte)(MessageVersion);
            Packet[5] = (byte)(MessageVersion >> 8);

            return Packet;
        }

        internal void KeepAliveMessageReceived()
        {
            this.LastKeepAlive = DateTime.UtcNow;
            Logging.Info(this.GetType(), "Keep-alive message received. Timestamp updated.");
        }
    }
}
