using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class JSToCSharpPorter
{
    // Dictionary to map JavaScript methods to C# methods
    private Dictionary<string, string> methodMap = new Dictionary<string, string>
    {
        { "writeIString", "WriteString" },
        { "writeInt32", "WriteInt" },
        { "writeRrsInt32", "WriteVInt" }
    };

    // Method to perform the transformation from JS to C#
    public string Transform(string jsCode)
    {
        // Step 1: Replace "this.data" with "Stream"
        string transformedCode = jsCode.Replace("this.data", "Stream");

        // Step 2: Regular expression to match function calls in JavaScript (like `.writeIString(`)
        var regex = new Regex(@"\.([a-zA-Z0-9_]+)\s*\("); // Matches `.writeIString(`, `.writeInt32(`, etc.

        // Step 3: Replace matched function calls with the C# equivalent
        transformedCode = regex.Replace(transformedCode, match =>
        {
            string jsMethod = match.Groups[1].Value;

            if (methodMap.ContainsKey(jsMethod))
            {
                // Replace the JS method with the C# method
                return $".{methodMap[jsMethod]}(";
            }
            return match.Value;  // If no mapping found, keep the original method name
        });

        return transformedCode;
    }
}

public class Program
{
    public static void Main()
    {
        // Your existing JS code
        string jsCode = @"
        this.data.writeInt32(this.device.player.highID);
        this.data.writeInt32(this.device.player.lowID);
        this.data.writeRrsInt32(14);//Unk (3)
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(0);//Tick 1 (1727700)
        this.data.writeRrsInt32(0);//Tick 2 (1727700)
        this.data.writeRrsInt32(Date.now());

        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(this.device.player.decks.length);
        this.device.player.decks.forEach(deck => {
            this.data.writeRrsInt32(deck.length);
            deck.forEach(card => {
                this.data.writeRrsInt32(card);
            });
        });

        this.data.writeByte(0xFF);
        let currentDeck = this.device.player.decks[this.device.player.selectedDeck];
        currentDeck.forEach(cardSCID => {
            let card = utils.findObjectByKey(this.device.player.cards, 'ID', cardUtils.SCIDtoInstanceID(cardSCID));

            this.data.writeRrsInt32(card.ID);
            this.data.writeRrsInt32(card.level);
            this.data.writeRrsInt32(0);
            this.data.writeRrsInt32(card.xpPoints);
            this.data.writeRrsInt32(0);
            this.data.writeRrsInt32(0);
            this.data.writeRrsInt32(0);
            this.data.writeRrsInt32(0);
        });

        this.data.writeRrsInt32(this.device.player.cards.length - 8);

        this.device.player.cards.forEach(card => {
            if (!currentDeck.includes(cardUtils.instanceIDtoSCID(card.ID))) {
                this.data.writeRrsInt32(card.ID);
                this.data.writeRrsInt32(card.level);
                this.data.writeRrsInt32(0);
                this.data.writeRrsInt32(card.xpPoints);
                this.data.writeRrsInt32(0);
                this.data.writeRrsInt32(0);
                this.data.writeRrsInt32(0);
                this.data.writeRrsInt32(0);
            }
        });

        this.data.writeRrsInt32(this.device.player.selectedDeck);
        this.data.writeRrsInt32(0);

        this.data.writeByte(127);
        this.data.writeByte(127);
        this.data.writeByte(127);
        this.data.writeRrsInt32(Date.now());
        this.data.writeRrsInt32(1);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(6);


        this.data.writeRrsInt32(1109);
        this.data.writeIString('2v2 Button');
        this.data.writeRrsInt32(8);
        this.data.writeRrsInt32(1503298800);
        this.data.writeRrsInt32(1534834800);
        this.data.writeRrsInt32(1502694000);
        this.data.writeInt32(0);
        this.data.writeInt32(0);
        this.data.writeIString('2v2 Button');
        this.data.writeIString('{""HideTimer"":true,""HidePopupTimer"":true,""ExtraGameModeChance"":0,""GameMode"":""TeamVsTeamLadder"",""ExtraGameMode"":""None""}')
        this.data.writeByte(0);

        this.data.writeRrsInt32(1305);
        this.data.writeIString('1v1 Draft Friendly ');
        this.data.writeRrsInt32(5);
        this.data.writeRrsInt32(1517472000);
        this.data.writeRrsInt32(1522569600);
        this.data.writeRrsInt32(1517472000);
        this.data.writeInt32(0);
        this.data.writeInt32(0);
        this.data.writeIString('1v1 Draft Friendly ');
        this.data.writeIString('{""Title"":""Amichevole strategica 1v1"",""Subtitle"":""Gioca coi membri del clan!"",""GameMode"":""DraftMode"",""Background"":{""Path"":""/62ce7186-d295-4100-9b4e-58d86d3a854c_friendly_draft.png"",""Checksum"":""71c0065635a551b109400a766cbdabfc"",""File"":""friendly_draft.png""},""DraftDeck"":""Draft_v1""}')
        this.data.writeByte(0);

        this.data.writeRrsInt32(1343);
        this.data.writeIString('Clash Royale League Challenge Header');//to start again
        this.data.writeRrsInt32(13);
        this.data.writeRrsInt32(1521014400);
        this.data.writeRrsInt32(1521446400);
        this.data.writeRrsInt32(1521014400);
        this.data.writeInt32(0);
        this.data.writeInt32(0);
        this.data.writeIString('Clash Royale League Challenge Header');
        this.data.writeIString('{""Image"":{""Path"":""/5231460d-4307-4a3e-b989-8bbf2e6f8db7_cr_league_challenge_header.png"",""Checksum"":""45440c7c948e9175f69030bde7d2c630"",""File"":""cr_league_challenge_header.png""},""TitleGlow"":true,""Icon"":""icon_tournament_special_grand"",""Title"":""Clash Royale League""}')
        this.data.writeByte(0);

        this.data.writeRrsInt32(1344);
        this.data.writeIString('Clash Royale League Challenge');
        this.data.writeRrsInt32(2);
        this.data.writeRrsInt32(1521014400);
        this.data.writeRrsInt32(1521446400);
        this.data.writeRrsInt32(1520841600);
        this.data.writeInt32(0);
        this.data.writeInt32(0);
        this.data.writeIString('Clash Royale League Challenge');
        this.data.writeIString('{""GameMode"":""Challenge"",""Title"":""Sfida Clash Royale League"",""FreePass"":3,""JoinCost"":10,""JoinCostResource"":""Gems"",""MaxLosses"":3,""Rewards"":[{""Gold"":20,""Cards"":2},{""Gold"":30,""Cards"":3},{""Gold"":50,""Milestone"":{""Type"":""Gold"",""Amount"":500},""Cards"":5},{""Gold"":80,""Cards"":8},{""Gold"":120,""Milestone"":{""Type"":""RandomSpell"",""Amount"":15,""RandomSpell"":""Rare""},""Cards"":12},{""Gold"":170,""Cards"":17},{""Gold"":230,""Milestone"":{""Type"":""Gold"",""Amount"":1500},""Cards"":23},{""Gold"":300,""Cards"":30},{""Gold"":380,""Milestone"":{""Chest"":""Giant_<max_arena>"",""Type"":""Chest""},""Cards"":38},{""Gold"":470,""Cards"":47},{""Gold"":570,""Milestone"":{""Type"":""Gold"",""Amount"":5000},""Cards"":57},{""Gold"":680,""Cards"":68},{""Gold"":800,""Milestone"":{""Chest"":""Magic_<max_arena>"",""Type"":""Chest""},""Cards"":80},{""Gold"":930,""Cards"":93},{""Gold"":1070,""Milestone"":{""Type"":""Gold"",""Amount"":10000},""Cards"":107},{""Gold"":1220,""Cards"":122},{""Gold"":1400,""Milestone"":{""Type"":""RandomSpell"",""Amount"":10,""RandomSpell"":""Epic""},""Cards"":140},{""Gold"":1650,""Cards"":165},{""Gold"":2000,""Milestone"":{""Chest"":""Legendary"",""Type"":""Chest""},""Cards"":200},{""Gold"":2450,""Cards"":245},{""Gold"":3000,""Milestone"":{""IsHighlighted"":true,""Type"":""Gold"",""Amount"":200000},""Cards"":300}],""IconExportName"":""icon_tournament_special_grand"",""WinIconExportName"":""tournament_open_wins_badge_gold"",""Subtitle"":""Ecco come funziona"",""Description"":""Arriva la Clash Royale League: la tua occasione per diventare un giocatore professionista! Raggiungi 20 vittorie per proseguire nella competizione. Troverai altre informazioni nella tua posta in arrivo in gioco!"",""Background"":{""Path"":""/da8810e1-e57c-4d9e-a854-9dd41b38fd81_cr_league_challenge.png"",""Checksum"":""a1d8b04a8e911ed1e21b0aba5cb25b85"",""File"":""cr_league_challenge.png""},""Background_Complete"":{""Path"":""/da8810e1-e57c-4d9e-a854-9dd41b38fd81_cr_league_challenge.png"",""Checksum"":""3e1910cc44d36e1bacbb2b26ec3b51cf"",""File"":""cr_league_challenge.png""},""UnlockedForXP"":""Experienced"",""EndNotification"":""La sfida Clash Royale League termina tra 2 ore!""}')
        this.data.writeByte(0);

        this.data.writeRrsInt32(1345);
        this.data.writeIString('Boosts');
        this.data.writeRrsInt32(10);
        this.data.writeRrsInt32(1521187200);
        this.data.writeRrsInt32(1521446400);
        this.data.writeRrsInt32(1521187200);
        this.data.writeInt32(0);
        this.data.writeInt32(0);
        this.data.writeIString('Boosts');
        this.data.writeIString('{""Title"":""Potenziamenti!"",""Boosts"":[{""Type"":""VictoryGold"",""durationInHours"":168,""Cost"":300},{""Type"":""CrownChestCards"",""durationInHours"":168,""Cost"":300},{""Type"":""ChestSpeedUp"",""durationInHours"":168,""Cost"":300}]}');
        this.data.writeByte(0);

        this.data.writeRrsInt32(1346);
        this.data.writeIString('Barbarian Barrel Draft Challenge ');
        this.data.writeRrsInt32(2);
        this.data.writeRrsInt32(1521792000);
        this.data.writeRrsInt32(1522051200);
        this.data.writeRrsInt32(1521705600);
        this.data.writeInt32(0);
        this.data.writeInt32(0);
        this.data.writeIString('Barbarian Barrel Draft Challenge ');
        this.data.writeIString('{""Casual"":false,""GameMode"":""CardReleaseDraft"",""Title"":""Sfida strategica barile barbarico"",""FreePass"":1,""JoinCost"":100,""JoinCostResource"":""Gems"",""MaxLosses"":3,""Rewards"":[{""Gold"":700,""Cards"":10},{""Gold"":950,""Cards"":15},{""Gold"":1250,""Milestone"":{""Type"":""Gold"",""Amount"":1000},""Cards"":25},{""Gold"":1600,""Cards"":43},{""Gold"":2000,""Milestone"":{""Type"":""RandomSpell"",""Amount"":20,""RandomSpell"":""Rare""},""Cards"":65},{""Gold"":2500,""Cards"":93},{""Gold"":3100,""Milestone"":{""Type"":""Gold"",""Amount"":3000},""Cards"":125},{""Gold"":3800,""Cards"":165},{""Gold"":4650,""Milestone"":{""Chest"":""Giant_<max_arena>"",""Type"":""Chest""},""Cards"":210},{""Gold"":5750,""Cards"":265},{""Gold"":7100,""Milestone"":{""Type"":""Gold"",""Amount"":9000},""Cards"":335},{""Gold"":8750,""Cards"":430},{""Gold"":11000,""Milestone"":{""IsHighlighted"":true,""Type"":""Spell"",""Amount"":10,""Spell"":""BarbLog""},""Cards"":550}],""CardTheme"":""BarbLog"",""IconExportName"":""icon_tournament_card_release_grand"",""WinIconExportName"":""tournament_open_wins_badge_gold"",""Arena"":""All"",""Description"":""Scegli 4 carte e ricevine 4 dall\'avversario: uno dei due potrÃ  schierare il barile barbarico! Sblocca la nuova carta con 12 vittorie, ma occhio: con 3 sconfitte sei fuori!"",""EndNotification"":""La sfida strategica barile barbarico termina tra due ore! Prova a sbloccare la nuova carta!"",""StartNotification"":""La sfida strategica barile barbarico Ã¨ iniziata! Sblocca subito una nuova carta!"",""UnlockedForXP"":""Experienced"",""DraftDeck"":""Draft_v1"",""DraftDeckCardThemeSetOverride"":""Draft_AllRandom"",""Subtitle"":""Ecco come funziona"",""Subtitle_Short"":""Sblocca una nuova carta!"",""Background"":{""Path"":""/78096e87-fc08-434f-8387-93a51be1b27e_barbarian_barrel_challenge.png"",""Checksum"":""62bc57e2a6827d434c0ce8a604c3f586"",""File"":""barbarian_barrel_challenge.png""},""Background_Complete"":{""Path"":""/78096e87-fc08-434f-8387-93a51be1b27e_barbarian_barrel_challenge.png"",""Checksum"":""25c6e25d7f322acef312ab50b009b68c"",""File"":""barbarian_barrel_challenge.png""}}');
        this.data.writeByte(0);

        this.data.append([0, 0, 0, 0, 0, 0])
        this.data.writeByte(127);
        this.data.writeByte(127);
        this.data.writeByte(127);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(0);

        this.data.writeRrsInt32(6); //Events count again

        this.data.writeRrsInt32(1109);
        this.data.writeRrsInt32(1);

        this.data.writeRrsInt32(1305);
        this.data.writeRrsInt32(1);

        this.data.writeRrsInt32(1343);
        this.data.writeRrsInt32(1);

        this.data.writeRrsInt32(1344);
        this.data.writeRrsInt32(1);

        this.data.writeRrsInt32(1345);
        this.data.writeRrsInt32(1);

        this.data.writeRrsInt32(1346);
        this.data.writeRrsInt32(1);

        this.data.writeRrsInt32(6);//Events count again, what's wrong with you Supercell :p

        this.data.writeRrsInt32(1109);
        this.data.writeRrsInt32(2);
        this.data.writeRrsInt32(0);

        this.data.writeRrsInt32(1305);
        this.data.writeRrsInt32(2);
        this.data.writeRrsInt32(0);

        this.data.writeRrsInt32(1343);
        this.data.writeRrsInt32(3);
        this.data.writeRrsInt32(0);

        this.data.writeRrsInt32(1344);
        this.data.writeRrsInt32(3);
        this.data.writeRrsInt32(0);

        this.data.writeRrsInt32(1345);
        this.data.writeRrsInt32(3);
        this.data.writeRrsInt32(0);

        this.data.writeRrsInt32(1346);
        this.data.writeRrsInt32(1);
        this.data.writeRrsInt32(0);

        this.data.writeRrsInt32(1);

        this.data.writeIString('{""ID"":""SHOP_CYCLE_MANAGEMENT"",""Params"":{""LegendaryChestCycleDuration"":14}}....Ú{""ID"":""CARD_RELEASE_V2"",""Params"":{""Cards"":[{""ShowAsSoon"":false,""Spell"":""Ghost"",""Date"":""20180104""},{""ShowAsSoon"":false,""Spell"":""EliteArcher"",""Date"":""20180302""},{""ShowAsSoon"":false,""Spell"":""BarbLog"",""Date"":""20180406""}]}}');
        this.data.writeRrsInt32(1);
        this.data.writeIString('{""ID"":""CARD_RELEASE_V2"",""Params"":{""Cards"":[{""ShowAsSoon"":false,""Spell"":""Ghost"",""Date"":""20180104""},{""ShowAsSoon"":false,""Spell"":""EliteArcher"",""Date"":""20180302""},{""ShowAsSoon"":false,""Spell"":""BarbLog"",""Date"":""20180406""}]}}');
        this.data.writeRrsInt32(4);
        this.data.writeIString('{""ID"":""CLAN_CHEST"",""Params"":{""StartTime"":""20170317T070000.000Z"",""ActiveDuration"":""P3dT0h"",""InactiveDuration"":""P4dT0h"",""ChestType"":[""ClanCrowns""]}}');
        this.data.writeRrsInt32(1);
        this.data.writeIString('{""ID"":""PERMANENT_GAME_MODES"",""Params"":{""FixedDeckOrderOptionEnabled"":true}}');


        this.data.writeRrsInt32(4); //Chest Slots Count

        if (this.device.player.chests && this.device.player.chests.length > 0 && this.device.player.chests.length <= 4) {
            this.data.writeRrsInt32(1);
            this.device.player.chests.forEach(chest => {
                this.data.writeRrsInt32(19);
                this.data.writeRrsInt32(chest.chestID);

                this.data.writeRrsInt32(10); //Is opened??? 0 no 2 yep 4???? 6 opened but locked 8 new with anim 10 new with animation and opened 12 new with animation and closed 14 new with animation and opened but locked 16 unlocking
                this.data.writeRrsInt32(this.device.player.chests.indexOf(chest) * 2 + 4); //Chest Index
                this.data.writeRrsInt32(6);

                this.data.writeRrsInt32(this.device.player.chests.indexOf(chest));
                this.data.writeRrsInt32(0);
                this.data.writeRrsInt32(0);
                if (this.device.player.chests) {
                    this.data.writeRrsInt32(this.device.player.chests.indexOf(chest) === (this.device.player.chests.length - 1) ? 0 : 8);
                }
            });
        }
        else {
            this.data.writeRrsInt32(0);
        }

        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(Date.now());
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(0);
        this.data.writeByte(127);

        this.data.writeRrsInt32(1);//Count???
        this.data.writeRrsInt32(19);
        this.data.writeRrsInt32(7);
        this.data.writeRrsInt32(2);
        this.data.writeRrsInt32(2);
        this.data.writeRrsInt32(0);
        this.data.writeByte(127);//Maybe end...

        this.data.append([0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]);
        this.data.writeRrsInt32(10);//Crown Chest Crowns Count
        this.data.writeRrsInt32(0);//Has been opened (bool)

        this.data.writeRrsInt32(0);//2x(2xTick + Timestamp)
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(Date.now());
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(Date.now());

        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(0);
        this.data.writeByte(127);

        this.data.writeRrsInt32(2817);
        this.data.append([0, 0, 0, 0, 0, 0, 0]);
        this.data.writeRrsInt32(2);
        this.data.writeRrsInt32(this.device.player.level);
        this.data.writeRrsInt32(54);
        this.data.writeRrsInt32(1);
        this.data.writeRrsInt32(5);
        this.data.writeRrsInt32(447);
        this.data.writeRrsInt32(5);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(Date.now());

        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(0);
        this.data.writeByte(127);

        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(0);
        this.data.writeByte(127);

        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(0);
        this.data.writeByte(127);

        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(1);

        this.data.append([0, 0, 0, 0, 0, 0, 0, 0]);
        this.data.writeRrsInt32(10);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(0);


        this.data.append(Buffer.from('F807', 'hex'));
        currentDeck.forEach(cardSCID => {
            let card = utils.findObjectByKey(this.device.player.cards, 'ID', cardUtils.SCIDtoInstanceID(cardSCID));

            this.data.writeRrsInt32(card.ID);
            this.data.writeRrsInt32(card.level);
            this.data.writeRrsInt32(0);
            this.data.writeRrsInt32(card.xpPoints);
            this.data.writeRrsInt32(0);
            this.data.writeRrsInt32(0);
            this.data.writeRrsInt32(0);
            this.data.writeRrsInt32(0);
        });

        this.data.writeRrsInt32(1);
        this.data.writeRrsInt32(26);
        this.data.writeRrsInt32(62);
        this.data.writeRrsInt32(1);
        this.data.writeRrsInt32(26);
        this.data.writeRrsInt32(62);

        this.data.append([0, 0, 0, 0, 0, 0]);

        this.data.writeRrsInt32(1);
        this.data.writeRrsInt32(Date.now());

        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(1);

        this.data.append([0, 0, 0, 0, 0, 0, 0]);

        this.data.writeRrsInt32(2);
        this.data.writeRrsInt32(1);
        this.data.writeRrsInt32(2);
        this.data.writeRrsInt32(1);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(0);

        //-- Quests --//
        this.data.append(Buffer.from('0000000F5449445F4441494C595F4749465453000000000300000000001151756573745F4672656547696674733130000000145449445F465245455F43484553545F51554553540000000873632F75692E73630000001571756573745F6974656D5F667265655F636865737405031307011307010501900103000000030004040000020100000000155449445F4C41444445525F51554553545F504C4159000000000A01000000001A506C61795F51756573745F506C61795F4C61646465725F5076500000001A5449445F4C41444445525F51554553545F504C41595F494E464F0000000873632F75692E73630000000E71756573745F6974656D5F70767014010E000A0000010100000032010000BF060201000301000000000000000000000000000000', 'hex'));

        //-- Shop Begins --//
        this.data.writeRrsInt32(14);
        this.data.writeRrsInt32(4);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(4);
        this.data.writeRrsInt32(1);
        this.data.writeRrsInt32(4);
        this.data.writeRrsInt32(1);
        this.data.writeRrsInt32(4);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(5);
        this.data.writeRrsInt32(2);
        this.data.writeRrsInt32(4);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(4);
        this.data.writeRrsInt32(2);
        this.data.writeRrsInt32(4);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(5);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(2);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(4);
        this.data.writeRrsInt32(3);
        this.data.writeRrsInt32(4);
        this.data.writeRrsInt32(2);
        this.data.writeRrsInt32(5);
        this.data.writeRrsInt32(3);
        this.data.writeRrsInt32(2);
        this.data.writeRrsInt32(3);
        this.data.writeRrsInt32(1);
        this.data.writeRrsInt32(8);
        this.data.writeByte(127);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(5);
        this.data.writeRrsInt32(3);
        this.data.writeRrsInt32(3);

        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(447);
        this.data.writeRrsInt32(0);//Cost
        this.data.writeRrsInt32(5);//SCID Res Type - Resources
        this.data.writeRrsInt32(1);//SCID Res ID - Gold
        this.data.writeRrsInt32(19);//SCID Res Type - Chests
        this.data.writeRrsInt32(114);//SCID Res ID - Chest ID
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(1);

        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(1);
        this.data.writeRrsInt32(447);
        this.data.writeRrsInt32(50);
        this.data.writeRrsInt32(5);
        this.data.writeRrsInt32(1);
        this.data.writeRrsInt32(26);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(5);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(3000);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(1);

        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(2);
        this.data.writeRrsInt32(447);
        this.data.writeRrsInt32(100);
        this.data.writeRrsInt32(5);
        this.data.writeRrsInt32(1);
        this.data.writeRrsInt32(28);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(1);
        this.data.writeRrsInt32(1);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(3000);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(0);
        //-- Shop Ends --//

        //-- Shop Chests Begins --//
        this.data.writeRrsInt32(3); //Chests Count

        // #1
        this.data.writeRrsInt32(19);
        this.data.writeRrsInt32(306);
        this.data.writeRrsInt32(88);
        this.data.writeRrsInt32(0);
        this.data.writeIString('Lightning');
        this.data.writeRrsInt32(447);
        this.data.writeRrsInt32(0);//Chest index
        this.data.writeRrsInt32(0);
        this.data.writeByte(127);
        this.data.writeRrsInt32(0);
        this.data.writeByte(127);

        // #2
        this.data.writeRrsInt32(19);
        this.data.writeRrsInt32(318);
        this.data.writeRrsInt32(88);
        this.data.writeRrsInt32(1);
        this.data.writeIString('Fortune');
        this.data.writeRrsInt32(447);
        this.data.writeRrsInt32(1);//Chest index
        this.data.writeRrsInt32(0);
        this.data.writeByte(127);
        this.data.writeRrsInt32(0);
        this.data.writeByte(127);

        // #3
        this.data.writeRrsInt32(19);
        this.data.writeRrsInt32(330);
        this.data.writeRrsInt32(88);
        this.data.writeRrsInt32(3);
        this.data.writeIString('Kings');
        this.data.writeRrsInt32(447);
        this.data.writeRrsInt32(2);//Chest index
        this.data.writeRrsInt32(0);
        this.data.writeByte(127);
        this.data.writeRrsInt32(0);
        this.data.writeByte(127);
        //-- Shop Chests Ends --//

        this.data.append([0, 0, 0, 0, 0, 0, 0]);
        this.data.writeByte(127);
        this.data.writeRrsInt32(1109);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(0);

        this.data.writeRrsInt32(this.device.player.highID);
        this.data.writeRrsInt32(this.device.player.lowID);
        this.data.writeRrsInt32(this.device.player.highID);
        this.data.writeRrsInt32(this.device.player.lowID);
        this.data.writeRrsInt32(this.device.player.highID);
        this.data.writeRrsInt32(this.device.player.lowID);
        this.data.writeIString(this.device.player.name);

        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(2);
        this.data.writeRrsInt32(this.device.player.trophies);
        this.data.append([0, 0, 0, 0, 0, 0, 0, 0]);
        this.data.writeRrsInt32(41);
        this.data.append([0, 0, 0, 0, 0]);

        this.data.writeRrsInt32(8); //Resources Prefix???
        this.data.writeRrsInt32(14); //Resources Count

        this.data.writeRrsInt32(5);//SCID Resource Type (Resources)
        this.data.writeRrsInt32(1);//SCID Resource ID (Gold)
        this.data.writeRrsInt32(this.device.player.gold);

        this.data.writeRrsInt32(5);//SCID Resource Type (Resources)
        this.data.writeRrsInt32(5);//SCID Resource ID (Gold)
        this.data.writeRrsInt32(this.device.player.gold);

        this.data.writeRrsInt32(5);//SCID Resource Type (Resources)
        this.data.writeRrsInt32(29);//SCID Resource ID (GameMode)
        this.data.writeRrsInt32(72000006);

        this.data.writeRrsInt32(5);//SCID Resource Type (Resources)
        this.data.writeRrsInt32(13);//SCID Resource ID (Unknown)
        this.data.writeRrsInt32(0);

        this.data.writeRrsInt32(5);//SCID Resource Type (Resources)
        this.data.writeRrsInt32(33);//SCID Resource ID (Unknown)
        this.data.writeRrsInt32(0);

        this.data.writeRrsInt32(5);//SCID Resource Type (Resources)
        this.data.writeRrsInt32(34);//SCID Resource ID (Unknown)
        this.data.writeRrsInt32(0);

        this.data.writeRrsInt32(5);//SCID Resource Type (Resources)
        this.data.writeRrsInt32(14);//SCID Resource ID (Unknown)
        this.data.writeRrsInt32(5);

        this.data.writeRrsInt32(5);//SCID Resource Type (Resources)
        this.data.writeRrsInt32(38);//SCID Resource ID (Unknown)
        this.data.writeRrsInt32(0);
        
        this.data.writeRrsInt32(5);//SCID Resource Type (Resources)
        this.data.writeRrsInt32(3);//SCID Resource ID (Unknown)
        this.data.writeRrsInt32(2);

        this.data.writeRrsInt32(5);//SCID Resource Type (Resources)
        this.data.writeRrsInt32(37);//SCID Resource ID (Unknown)
        this.data.writeRrsInt32(0);

        this.data.writeRrsInt32(5);//SCID Resource Type (Resources)
        this.data.writeRrsInt32(2);//SCID Resource ID (Unknown)
        this.data.writeRrsInt32(6);

        this.data.writeRrsInt32(5);//SCID Resource Type (Resources)
        this.data.writeRrsInt32(16);//SCID Resource ID (Unknown)
        this.data.writeRrsInt32(1);

        this.data.writeRrsInt32(5);//SCID Resource Type (Resources)
        this.data.writeRrsInt32(4);//SCID Resource ID (Unknown)
        this.data.writeRrsInt32(0);

        this.data.writeRrsInt32(5);//SCID Resource Type (Resources)
        this.data.writeRrsInt32(28);//SCID Resource ID (Unknown)
        this.data.writeRrsInt32(0);

        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(6);
        this.data.writeRrsInt32(60);

        this.data.writeRrsInt32(7);
        this.data.writeRrsInt32(9);
        this.data.writeRrsInt32(60);

        this.data.writeRrsInt32(8);
        this.data.writeRrsInt32(9);
        this.data.writeRrsInt32(60);

        this.data.writeRrsInt32(9);
        this.data.writeRrsInt32(9);
        this.data.writeRrsInt32(60);

        this.data.writeRrsInt32(4);
        this.data.writeRrsInt32(1);
        this.data.writeRrsInt32(60);

        this.data.writeRrsInt32(5);
        this.data.writeRrsInt32(1);
        this.data.writeRrsInt32(60);

        this.data.writeRrsInt32(6);
        this.data.writeRrsInt32(1);
        this.data.writeRrsInt32(0);

        this.data.writeRrsInt32(6);
        this.data.writeRrsInt32(5);
        this.data.writeRrsInt32(11);
        this.data.writeRrsInt32(41);
        this.data.writeRrsInt32(5);
        this.data.writeRrsInt32(8);
        this.data.writeRrsInt32(9);
        this.data.writeRrsInt32(5);
        this.data.writeRrsInt32(27);
        this.data.writeRrsInt32(1);
        this.data.writeRrsInt32(5);
        this.data.writeRrsInt32(7);
        this.data.writeRrsInt32(1);
        this.data.writeRrsInt32(5);
        this.data.writeRrsInt32(6);
        this.data.writeRrsInt32(30);
        this.data.writeRrsInt32(5);

        this.data.writeRrsInt32(9);
        this.data.writeRrsInt32(26000003);

        this.data.writeRrsInt32(9);
        this.data.writeRrsInt32(26);
        this.data.writeRrsInt32(3);

        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(26);
        this.data.writeRrsInt32(1);

        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(26);
        this.data.writeRrsInt32(13);

        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(26);
        this.data.writeRrsInt32(0);

        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(28);
        this.data.writeRrsInt32(1);

        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(28);
        this.data.writeRrsInt32(0);

        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(26);
        this.data.writeRrsInt32(12);

        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(26);
        this.data.writeRrsInt32(18);

        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(26);
        this.data.writeRrsInt32(14);


        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(this.device.player.diamonds);//Diamonds
        this.data.writeRrsInt32(this.device.player.diamonds);//Free Diamonds

        this.data.writeRrsInt32(this.device.player.xpPoints);//XPlevel
        this.data.writeRrsInt32(this.device.player.level);//Level
        this.data.writeRrsInt32(0);//Unk

        this.data.writeRrsInt32(1);//ChangeNameCount
        this.data.writeRrsInt32(1);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(1);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(1);
        this.data.writeRrsInt32(5);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(1);
        this.data.writeRrsInt32(0);
        this.data.writeRrsInt32(Date.now());
        this.data.writeRrsInt32(Date.now());
        this.data.writeRrsInt32(0);//Tick
        ";

        var porter = new JSToCSharpPorter();
        string csharpCode = porter.Transform(jsCode);

        // Output the transformed C# code
        Console.WriteLine("Transformed C# Code:");
        Console.WriteLine(csharpCode);

        // Prevent the console window from closing automatically
        Console.WriteLine("Press any key to exit.");
        Console.ReadKey();
    }
}
