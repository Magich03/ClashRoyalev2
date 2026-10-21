namespace ClashRoyale.Messages.Server.Account
{
    using ClashRoyale.Enums;
    using ClashRoyale.Extensions;
    using ClashRoyale.Logic.Player;
    using System;

    public class LoginOkMessage : Message
    {
        /// <summary>
        /// Gets the type of this message.
        /// </summary>
        public override short Type
        {
            get
            {
                return 22280;
            }
        }

        /// <summary>
        /// Gets the service node of this message.
        /// </summary>
        public override Node ServiceNode
        {
            get
            {
                return Node.Account;
            }
        }

        public Player Player;

        /// <summary>
        /// Initializes a new instance of the <see cref="LoginOkMessage"/> class.
        /// </summary>
        public LoginOkMessage()
        {
            this.Version = 1;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LoginOkMessage"/> class.
        /// </summary>
        /// <param name="Stream">The stream.</param>
        public LoginOkMessage(ByteStream Stream) : base(Stream)
        {
            this.Version = 1;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LoginOkMessage"/> class.
        /// </summary>
        /// <param name="Player">The player.</param>
        public LoginOkMessage(Player Player)
        {
            this.Version = 1;
            this.Player = Player;
        }

        /// <summary>
        /// Decodes this instance.
        /// </summary>
        public override void Decode()
        {
            this.Player.HighId = this.Stream.ReadInt();
            this.Player.LowId = this.Stream.ReadInt();

            this.Player.HighId = this.Stream.ReadInt();
            this.Player.LowId = this.Stream.ReadInt();

            this.Player.Token = this.Stream.ReadString();

            this.Stream.ReadString();
            this.Stream.ReadString();

            this.Stream.ReadVInt();
            this.Stream.ReadVInt();
            this.Stream.ReadVInt();
            this.Stream.ReadVInt();

            this.Stream.ReadString();

            this.Stream.ReadVInt();
            this.Stream.ReadVInt();
            this.Stream.ReadVInt();

            this.Stream.ReadString();
            this.Stream.ReadString();
            this.Stream.ReadString();

            this.Stream.ReadVInt();

            this.Stream.ReadString();
            this.Stream.ReadString();
            this.Stream.ReadString();

            for (int i = 0; i < this.Stream.ReadVInt(); i++)
            {
                this.Stream.ReadString();
            }

            for (int i = 0; i < this.Stream.ReadVInt(); i++)
            {
                this.Stream.ReadString();
            }

            for (int i = 0; i < this.Stream.ReadVInt(); i++)
            {
                this.Stream.ReadString();
            }
        }

        /// <summary>
        /// Encodes this instance.
        /// </summary>
        public override void Encode()
        {
            // Write Player IDs
            this.Stream.WriteInt(this.Player.HighId);
            this.Stream.WriteInt(this.Player.LowId);
            this.Stream.WriteInt(this.Player.HighId);
            this.Stream.WriteInt(this.Player.LowId);

            // Write Player Token
            this.Stream.WriteString(this.Player.Token);

            // Write Placeholder Strings
            this.Stream.WriteString(""); // Facebook ID placeholder
            this.Stream.WriteString(""); // Gamecenter ID placeholder

            // Write Server Version Information
            this.Stream.WriteVInt(Config.ClientMajorVersion);
            this.Stream.WriteVInt(Config.ClientMinorVersion);
            this.Stream.WriteVInt(Config.ClientMinorVersion);
            this.Stream.WriteVInt(Config.ClientBuildVersion);

            // Write Server Type
            this.Stream.WriteString(Config.Environment);

            // Write Session Details
            this.Stream.WriteVInt(0); // Session count
            this.Stream.WriteVInt(0); // Play time in seconds
            this.Stream.WriteVInt(0); // Days since started playing

            // Write Facebook ID and Dates
            this.Stream.WriteString("1475268786112433"); // Facebook ID
            this.Stream.WriteString(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()); // Server time
            this.Stream.WriteString(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()); // Account created date

            // Write Optional Byte
            this.Stream.WriteByte(0);

            // Write Empty Placeholder Strings
            this.Stream.WriteString(""); // Placeholder 1
            this.Stream.WriteString(""); // Placeholder 2
            this.Stream.WriteString(""); // Placeholder 3

            // Write Location Details
            this.Stream.WriteString("IT"); // State
            this.Stream.WriteString("CR City"); // City
            this.Stream.WriteString("20"); // Country code

            // Write Additional Details
            this.Stream.WriteVInt(1); // Additional count
            this.Stream.WriteVInt(455500); // Additional value 1
            this.Stream.WriteVInt(115500); // Additional value 2

            // Write Asset URLs
            this.Stream.WriteVInt(2); // Asset URL count
            this.Stream.WriteString(""); // Asset URL 1
            this.Stream.WriteString("https://99faf1e355c749a9a049-2a63f4436c967aa7d355061bd0d924a1.ssl.cf1.rackcdn.com"); // Asset URL 2

            // Write Event Asset URLs
            this.Stream.WriteVInt(1); // Event asset URL count
            this.Stream.WriteString(""); // Event asset URL
        }
    }
}