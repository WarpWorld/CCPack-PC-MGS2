using System.Diagnostics.CodeAnalysis;
using System.Text;
using ConnectorLib.Memory;
using CrowdControl.Common;
using AddressChain = ConnectorLib.Memory.AddressChain<ConnectorLib.Inject.InjectConnector>;
using ConnectorType = CrowdControl.Common.ConnectorType;
using Log = CrowdControl.Common.Log;

namespace CrowdControl.Games.Packs.MGS2;

[SuppressMessage("ReSharper", "StringLiteralTypo")]
public class MGS2 : InjectEffectPack
{
    public override Game Game { get; } = new("METAL GEAR SOLID2", "MGS2", "PC", ConnectorType.PCConnector);

    public MGS2(UserRecord player, Func<CrowdControlBlock, bool> responseHandler, Action<object> statusUpdateHandler)
        : base(player, responseHandler, statusUpdateHandler)
    {
        VersionProfiles = [new("METAL GEAR SOLID2", InitGame, DeinitGame)];
    }

    #region AddressChains

    // Game State Checks
    private AddressChain characterString;
    private AddressChain mapString;
    private AddressChain pauseState;

    // Alert Statuses
    private AddressChain alertTimer;
    private AddressChain evasionTimer;
    private AddressChain alertStatusTrigger;

    // Camera and HUD
    private AddressChain letterBoxMode;
    private AddressChain dayOrNightMode;
    private AddressChain cameraZoom;
    private AddressChain hudFilter;
    private AddressChain disableItemMenuPause;
    private AddressChain disableWeaponMenuPause;

    private static readonly byte[] ItemMenuPauseDefaultBytes = [0x09, 0x2D, 0x1D, 0xCA, 0x5E, 0x01];
    private static readonly byte[] WeaponMenuPauseDefaultBytes = [0x09, 0x2D, 0x1F, 0xAF, 0x5E, 0x01];
    private static readonly byte[] MenuPauseDisabledBytes = [0x90, 0x90, 0x90, 0x90, 0x90, 0x90];

    // Guards
    private AddressChain guardAnimations;
    private AddressChain guardWakeStatus;
    private AddressChain guardSleepStatus;

    // Snake/Raiden
    private AddressChain flinchPlayer;
    private AddressChain stealthMode;

    // Weapons and Items
    private AddressChain weaponsAndItemPointer;
    private AddressChain equippedWeapon;
    private AddressChain equippedItem;
    private AddressChain weaponClipCount;

    #endregion

    #region [De]init

    private void InitGame()
    {
        Connector.PointerFormat = PointerFormat.Absolute64LE;

        // Game State Checks
        characterString = AddressChain.Parse(Connector, "\"METAL GEAR SOLID2.exe\"+949340=>+1C"); // Done
        mapString = AddressChain.Parse(Connector, "\"METAL GEAR SOLID2.exe\"+949340=>+2C"); // Done
        pauseState = AddressChain.Parse(Connector, "\"METAL GEAR SOLID2.exe\"+17DBC7C");// Done

        // Alert Stauses
        alertTimer = AddressChain.Parse(Connector, "\"METAL GEAR SOLID2.exe\"+16C9568"); // Done
        evasionTimer = AddressChain.Parse(Connector, "\"METAL GEAR SOLID2.exe\"+16C9584"); // Done
        alertStatusTrigger = AddressChain.Parse(Connector, "\"METAL GEAR SOLID2.exe\"+949340=>+11A"); // Done

        // Camera and HUD
        letterBoxMode = AddressChain.Parse(Connector, "\"METAL GEAR SOLID2.exe\"+15525CD");
        dayOrNightMode = AddressChain.Parse(Connector, "\"METAL GEAR SOLID2.exe\"+2D1AAF"); // Double check
        cameraZoom = AddressChain.Parse(Connector, "\"METAL GEAR SOLID2.exe\"+15525C9"); // Test
        disableItemMenuPause = AddressChain.Parse(Connector, "\"METAL GEAR SOLID2.exe\"+1EF259");
        disableWeaponMenuPause = AddressChain.Parse(Connector, "\"METAL GEAR SOLID2.exe\"+1F0D57");

        // Guards
        guardAnimations = AddressChain.Parse(Connector, "\"METAL GEAR SOLID2.exe\"+16EBFD"); // Test
        guardWakeStatus = AddressChain.Parse(Connector, "\"METAL GEAR SOLID2.exe\"+159EAD"); // Done
        guardSleepStatus = AddressChain.Parse(Connector, "\"METAL GEAR SOLID2.exe\"+159498"); // Done

        // Snake/Raiden
        flinchPlayer = AddressChain.Parse(Connector, "\"METAL GEAR SOLID2.exe\"+17DF660=>+A8");

        // Weapons, Ammo & Items
        weaponsAndItemPointer = AddressChain.Parse(Connector, "\"METAL GEAR SOLID2.exe\"+1540C20=>+0"); // Done
        equippedWeapon = AddressChain.Parse(Connector, "\"METAL GEAR SOLID2.exe\"+949340=>+104"); // Done
        equippedItem = AddressChain.Parse(Connector, "\"METAL GEAR SOLID2.exe\"+949340=>+106"); // Done
        weaponClipCount = AddressChain.Parse(Connector, "\"METAL GEAR SOLID2.exe\"+16E994C"); // Done
    }
    private void DeinitGame()
    {
        RestoreMenuPauseDefault();
    }

    #endregion

    #region Weapon and Items Class

    public class WeaponItemManager
    {
        private readonly MGS2 mainClass;

        public WeaponItemManager(MGS2 mainClass)
        {
            this.mainClass = mainClass;
        }

        private int GetWeaponOffset(Weapons weapon)
        {
            return ((int)weapon) * 2;
        }

        private int GetItemOffset(Items item)
        {
            return 0x90 + (((int)item) * 2);
        }

        // Not currently used but could be useful for future effects
        private int GetMaxAmmoOffset(Weapons maxWeapon)
        {
            return 0x2F4 + GetWeaponOffset(maxWeapon);
        }

        public AddressChain GetWeaponAddress(Weapons weapon)
        {
            return mainClass.weaponsAndItemPointer + GetWeaponOffset(weapon);
        }

        public AddressChain GetItemAddress(Items item)
        {
            return mainClass.weaponsAndItemPointer + GetItemOffset(item);
        }

        public short ReadWeaponAmmo(Weapons weapon)
        {
            AddressChain ammoChain = mainClass.weaponsAndItemPointer + GetWeaponOffset(weapon);
            return mainClass.Get16(ammoChain);
        }

        public void WriteWeaponAmmo(Weapons weapon, short ammoCount)
        {
            AddressChain ammoChain = mainClass.weaponsAndItemPointer + GetWeaponOffset(weapon);
            mainClass.Set16(ammoChain, ammoCount);
        }

        public short ReadItemCapacity(Items item)
        {
            var addr = GetItemAddress(item);
            return mainClass.Get16(addr);
        }

        public void WriteItemCapacity(Items item, short value)
        {
            var addr = GetItemAddress(item);
            mainClass.Set16(addr, value);
        }
    }

    #endregion

    #region Enums and Dictionary

    public enum Weapons
    {
        WEP_NONE = 0,
        WEP_M9,
        WEP_USP,
        WEP_SOCOM,
        WEP_PSG1,
        WEP_RGB6,
        WEP_NIKITA,
        WEP_STINGER,
        WEP_CLAYMORE,
        WEP_C4,
        WEP_CHAFF,
        WEP_STUNG,
        WEP_DMIC,
        WEP_HFBLADE,
        WEP_COOLANT,
        WEP_AKS74U,
        WEP_MAGAZINE,
        WEP_GRENADE,
        WEP_M4,
        WEP_PSG1T,
        WEP_DMIC2,
        WEP_BOOK = 21
    }

    public enum Items
    {
        ITM_NONE = 0,
        ITM_RATION,
        ITM_SCOPE,
        ITM_MEDICINE,
        ITM_BANDAGE,
        ITM_PENTAZEMIN,
        ITM_BDU,
        ITM_ARMOR,
        ITM_STEALTH,
        ITM_MINED,
        ITM_SENSA,
        ITM_SENSB,
        ITM_NVG,
        ITM_THERMG,
        ITM_SCOPE2,
        ITM_DGCAM,
        ITM_BOX1,
        ITM_CIGS,
        ITM_CARD,
        ITM_SHAVER,
        ITM_PHONE,
        ITM_CAMERA,
        ITM_BOX2,
        ITM_BOX3,
        ITM_WETBOX,
        ITM_APSENSR,
        ITM_BOX4,
        ITM_BOX5,
        ITM_RAZOR,
        ITM_SCMSUPR,
        ITM_AKSUPR,
        ITM_CAMERA2,
        ITM_BANDANA,
        ITM_DOGTAGS,
        ITM_MODISC,
        ITM_USPSUPR,
        ITM_SPWIG,
        ITM_WIGA,
        ITM_WIGB,
        ITM_WIGC,
        ITM_WIGD = 40
    }

    private bool IsPlayableStage(string stage)
    {
        return new string[] { 
            // Tanker Chapter
            "w00a", // Aft Deck
            "w00b", // Navigational Deck (Olga Fight)
            "w00c", // Navigational Deck (After Olga Fight)
            "w01a", // Deck A, Crew's Quarters
            "w01b", // Deck A, Crew's Quarters, Starboard
            "w01c", // Deck C, Crew's Quarters
            "w01d", // Deck D, Crew's Quarters
            "w01e", // Deck E, The Bridge
            "w01f", // Deck A, Crew's Lounge
            "w02a", // Engine Room
            "w03a", // Deck-2, Port
            "w03b", // Deck-2, Starboard
            "w04a", // Hold N. 1
            "w04b", // Hold N. 2
            "w04c", // Hold N. 3

            // Plant Chapter
            "w11a", // Strut A Sea Dock
            "w11b", // Strut A Sea Dock (Bomb Disposal)
            "w11c", // Strut A Sea Dock (Fortune Fight)
            "w12a", // Strut A Roof
            "w12c", // Strut A Roof (Last Bomb)
            "w12b", // Strut A Pump Room
            "w13a", // AB Connecting Bridge
            "w13b", // AB Connecting Bridge (With Sensor B)
            "w14a", // Strut B Transformer Room
            "w15a", // BC Connecting Bridge
            "w15b", // BC Connecting Bridge (After Stillman Cutscene)
            "w16a", // Strut C Dining Hall
            "w16b", // Strut C Dining Hall (After 'd014p01')
            "w17a", // CD Connecting Bridge
            "w18a", // Strut D Sediment Pool
            "w19a", // DE Connecting Bridge
            "w20a", // Strut E Parcel Room, 1F
            "w20b", // Strut E Heliport
            "w20c", // Strut E Heliport (Last Bomb)
            "w20d", // Strut E Heliport (After Ninja Cutscene)
            "w21a", // EF Connecting Bridge
            "w22a", // Strut F Warehouse
            "w23b", // FA Connecting Bridge
            "w24a", // Shell 1 Core
            "w24b", // Shell 1 Core B1
            "w24d", // Shell 1 Core B2, Computer's Room
            "w24c", // Shell 1 B1 Hall, Hostages Room
            "w25a", // Shell 1,2 Connecting Bridge
            "w25b", // Shell 1,2 Connecting Bridge (Destroyed)
            "w25c", // Strut L Perimeter
            "w25d", // KL Connecting Bridge
            "w28a", // Strut L Sewage Treatment Facility
            "w31a", // Shell 2 Core, 1F Air Purification Room
            "w31b", // Shell 2 Core, B1 Filtration Chamber NO1
            "w31c", // Shell 2 Core, B1 Filtration Chamber NO2 (Vamp Fight)
            "w31d", // Shell 2 Core, 1F Air Purification Room (w/Emma)
            "w32a", // Strut L Oil Fence
            "w32b", // Strut L Oil Fence (Vamp Fight)
            "w41a", // Arsenal Gear-Stomach
            "w42a", // Arsenal Gear-Jejunum
            "w43a", // Arsenal Gear-Ascending Colon
            "w44a", // Arsenal Gear-Ileum
            "w45a", // Arsenal Gear-Sigmoid Colon
            "w46a", // Arsenal Gear-Rectum
            "w51a", // Arsenal Gear (After Ray Battle)
            "w61a", // Federal Hall

            // Snake Tales
            "a00a", // Alt Deck
            "a00b", // Navigational Deck (Meryl Fight)
            "a00c", // Navigational Deck (After Meryl Fight UNUSED)
            "a01a", // Deck A, Crew's Quarters
            "a01b", // Deck A, Crew's Quarters, Starboard
            "a01c", // Deck C, Crew's Quarters
            "a01d", // Deck D, Crew's Quarters
            "a01e", // Deck E, The Bridge
            "a01f", // Deck A, Crew's Lounge
            "a02a", // Engine Room
            "a03a", // Deck-2, Port
            "a03b", // Deck-2, Starboard
            "a04a", // Hold N. 1
            "a04b", // Hold N. 2
            "a04c", // Hold N. 3
            "a11a", // Strut A Sea Dock
            "a11b", // Strut A Sea Dock (Bomb Disposal)
            "a11c", // Strut A Sea Dock (Fortune Fight)
            "a12a", // Strut A Roof
            "a12c", // Strut A Roof (Last Bomb)
            "a12b", // Strut A Pump Room
            "a13a", // AB Connecting Bridge
            "a13b", // AB Connecting Bridge (With Sensor B)
            "a14a", // Strut B Transformer Room
            "a15a", // BC Connecting Bridge
            "a15b", // BC Connecting Bridge (After Stillman Cutscene)
            "a16a", // Strut C Dining Hall
            "a16b", // Strut C Dining Hall (After 'd014p01')
            "a17a", // CD Connecting Bridge
            "a18a", // Strut D Sediment Pool
            "a19a", // DE Connecting Bridge
            "a20a", // Strut E Parcel Room, 1F
            "a20b", // Strut E Heliport
            "a20c", // Strut E Heliport (Last Bomb)
            "a20d", // Strut E Heliport (After Ninja Cutscene)
            "a21a", // EF Connecting Bridge
            "a22a", // Strut F Warehouse
            "a23b", // FA Connecting Bridge
            "a24a", // Shell 1 Core
            "a24b", // Shell 1 Core B1
            "a24d", // Shell 1 Core B2, Computer's Room
            "a24c", // Shell 1 B1 Hall, Hostages Room
            "a25a", // Shell 1,2 Connecting Bridge
            "a25b", // Shell 1,2 Connecting Bridge (Destroyed)
            "a25c", // Strut L Perimeter
            "a25d", // KL Connecting Bridge
            "a28a", // Strut L Sewage Treatment Facility
            "a31a", // Shell 2 Core, 1F Air Purification Room
            "a31b", // Shell 2 Core, B1 Filtration Chamber NO1
            "a31c", // Shell 2 Core, B1 Filtration Chamber NO2 (Vamp Fight)
            "a31d", // Shell 2 Core, 1F Air Purification Room (w/Emma)
            "a32a", // Strut L Oil Fence
            "a32b", // Strut L Oil Fence (Vamp Fight)
            "a41a", // Arsenal Gear-Stomach
            "a42a", // Arsenal Gear-Jejunum
            "a43a", // Arsenal Gear-Ascending Colon
            "a44a", // Arsenal Gear-Ileum
            "a45a", // Arsenal Gear-Sigmoid Colon
            "a46a", // Arsenal Gear-Rectum
            "a51a", // Arsenal Gear (After Ray Battle)
            "a61a", // Federal Hall

            // VR Missions Sneaking/Eliminate All
            "vs01a",
            "vs02a",
            "vs03a",
            "vs04a",
            "vs05a",
            "vs06A",
            "vs07a",
            "vs08a",
            "vs09A",
            "vs10A",

            // VR Missions Variety
            "sp01a",
            "sp02a",
            "sp03a",
            "sp04a",
            "sp05a",
            "sp06a",
            "sp07a",
            "sp08a",
            
            // VR Missions Streaking Mode
            "st02a",
            "st03a",
            "st04a",
            "st05a",

            // VR Missions First Person Mode
            "sp21",
            "sp22",
            "sp23",
            "sp24",
            "sp25",

            // VR Missions Weapons Mode
            "wp01a", // (USP/SOCOM)
            "wp02a", // (USP/SOCOM)
            "wp03a", // (USP/SOCOM)
            "wp04a", // (USP/SOCOM)
            "wp05a", // (USP/SOCOM)

            "wp11a", // (M4/AK74U)
            "wp12a", // (M4/AK74U)
            "wp13a", // (M4/AK74U)
            "wp14a", // (M4/AK74U)
            "wp15a", // (M4/AK74U)

            "wp21a", // (C4/CLAYMORE)
            "wp22a", // (C4/CLAYMORE)
            "wp23a", // (C4/CLAYMORE)
            "wp24a", // (C4/CLAYMORE)
            "wp25a", // (C4/CLAYMORE)

            "wp31a", // (GRENADE)
            "wp32a", // (GRENADE)
            "wp33a", // (GRENADE)
            "wp34a", // (GRENADE)
            "wp35a", // (GRENADE)

            "wp41a", // (PSG-1)
            "wp42a", // (PSG-1)
            "wp43a", // (PSG-1)
            "wp44a", // (PSG-1)
            "wp45a", // (PSG-1)

            "wp51a", // (STINGER)
            "wp52a", // (STINGER)
            "wp53a", // (STINGER)
            "wp54a", // (STINGER)
            "wp55a", // (STINGER)

            "wp61a", // (NIKITA)
            "wp62a", // (NIKITA)
            "wp63a", // (NIKITA)
            "wp64a", // (NIKITA)
            "wp65a", // (NIKITA)

            "wp71a", // (NO WEAPON/HF.BLADE)
            "wp72a", // (NO WEAPON/HF.BLADE)
            "wp73a", // (NO WEAPON/HF.BLADE)
            "wp74a", // (NO WEAPON/HF.BLADE)
            "wp75a", // (NO WEAPON/HF.BLADE)           
            }.Contains(stage);
    }

    private bool IsCutsceneOrMenu(string stage)
    {
        return new string[] {
            // Dev Menu and Others
            "select",
            "n_title",
            "mselect",
            "tales",
            "ending",

            // Special Cutscenes
            "museum",
            "webdemo",

            // Tanker Cutscenes
            "d00t",
            "d01t",
            "d04t",
            "d05t",
            "d10t",
            "d11t",
            "d12t",
            "d12t3",
            "d12t4",
            "d13t",
            "d14t", 

            // Plant Cutscenes
            "d001p01",
            "d001p02",
            "d005p01",
            "d005p03",
            "d010p01",
            "d012p01",
            "d014p01",
            "d021p01",
            "d036p03",
            "d036p05",
            "d045p01",
            "d046p01",
            "d053p01",
            "d055p01",
            "d063p01",
            "d065p02",
            "d070p01",
            "d070p09",
            "d070px9",
            "d078p01",
            "d080p01",
            "d080p06",
            "d080p07",
            "d080p08",
            "d082p01",

        }.Contains(stage);
    }

    #endregion

    #region Memory Getters and Setters

    byte Get8(AddressChain addr)
    {
        return addr.GetByte();
    }

    void Set8(AddressChain addr, byte val)
    {
        addr.SetByte(val);
    }

    short Get16(AddressChain addr)
    {
        return BitConverter.ToInt16(addr.GetBytes(2), 0);
    }

    void Set16(AddressChain addr, short val)
    {
        addr.SetBytes(BitConverter.GetBytes(val));
    }

    int Get32(AddressChain addr)
    {
        return BitConverter.ToInt32(addr.GetBytes(4), 0);
    }

    void Set32(AddressChain addr, int val)
    {
        addr.SetBytes(BitConverter.GetBytes(val));
    }

    float GetFloat(AddressChain addr)
    {
        if (addr.TryGetBytes(4, out byte[] bytes))
        {
            return BitConverter.ToSingle(bytes, 0);
        }
        else
        {
            throw new Exception("Failed to read float value.");
        }
    }

    void SetFloat(AddressChain addr, float val)
    {
        byte[] bytes = BitConverter.GetBytes(val);
        addr.SetBytes(bytes);
    }

    T[] GetArray<T>(AddressChain addr, int count) where T : struct
    {
        int typeSize = System.Runtime.InteropServices.Marshal.SizeOf<T>();
        int totalSize = typeSize * count;
        byte[] bytes = addr.GetBytes(totalSize);

        T[] array = new T[count];
        Buffer.BlockCopy(bytes, 0, array, 0, totalSize);
        return array;
    }

    void SetArray<T>(AddressChain addr, T[] values) where T : struct
    {
        int typeSize = System.Runtime.InteropServices.Marshal.SizeOf<T>();
        int totalSize = typeSize * values.Length;
        byte[] bytes = new byte[totalSize];
        Buffer.BlockCopy(values, 0, bytes, 0, totalSize);
        addr.SetBytes(bytes);
    }

    public static short SetSpecificBits(short currentValue, int startBit, int endBit, int valueToSet)
    {
        int maskLength = endBit - startBit + 1;
        int mask = ((1 << maskLength) - 1) << startBit;
        return (short)((currentValue & ~mask) | ((valueToSet << startBit) & mask));
    }

    private string GetString(AddressChain addr, int maxLength)
    {
        if (maxLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxLength), "maxLength must be positive");

        byte[] data = addr.GetBytes(maxLength);
        if (data == null || data.Length == 0)
        {
            return string.Empty;
        }

        int nullIndex = Array.IndexOf(data, (byte)0);
        if (nullIndex >= 0)
        {
            return Encoding.ASCII.GetString(data, 0, nullIndex);
        }
        else
        {
            return Encoding.ASCII.GetString(data, 0, data.Length);
        }
    }

    protected override bool IsReady(EffectRequest request)
    {
        return GetGameState() == GameState.Ready;
    }

    protected override GameState GetGameState()
    {
        try
        {
            string currentStage = GetMapString();
            if (string.IsNullOrWhiteSpace(currentStage))
                return GameState.Unknown;

            byte pauseStateValue = GetPauseState();
            if (IsCutsceneOrMenu(currentStage) || (pauseStateValue == 1) || (pauseStateValue == 2) || (pauseStateValue == 4))
            {
                return GameState.WrongMode;
            }

            return GameState.Ready;
        }
        catch (Exception ex)
        {
            Log.Error($"GetGameState failed: {ex.Message}");
            return GameState.Unknown;
        }
    }


    #endregion

    #region Effect Helpers

    #region Game State Tracking

    private string currentCharacter = string.Empty;
    private string currentMap = string.Empty;
    private int guardAnimationToken = 0;
    private bool isTimedGuardAnimationActive = false;
    private readonly Random guardAnimationRandom = new();

    private string GetCharacterString()
    {
        string currentCharacter = GetString(characterString, 8);
        Log.Message($"Current Character: {currentCharacter}");
        return currentCharacter;
    }

    private string GetMapString()
    {
        string currentMap = GetString(mapString, 8);
        Log.Message($"Current Map: {currentMap}");
        return currentMap;
    }

    private byte GetPauseState()
    {
        byte pauseStateValue = Get8(pauseState);
        Log.Message($"Pause State: {pauseStateValue}");
        return pauseStateValue;
    }

    private void StartTimedGuardAnimationEffect(EffectRequest request, Action setAnimationAction, string animationName)
    {
        if (isTimedGuardAnimationActive)
        {
            Respond(request, EffectStatus.FailTemporary, StandardErrors.AlreadyInState, ["Guard animations", "already modified"]);
            return;
        }

        System.TimeSpan duration = TimeSpan.FromSeconds(request.Duration.TotalSeconds);
        int thisToken = ++guardAnimationToken;

        TryEffect(request,
            () => true,
            () =>
            {
                setAnimationAction();
                isTimedGuardAnimationActive = true;
                return true;
            },
            () => Connector.SendMessage(text: $"{request.DisplayViewer} set the guard animations to {animationName} for {duration.TotalSeconds} seconds."));

        _ = ResetGuardAnimationsAfterDelay(thisToken, duration);
    }

    private void SetRandomGuardAnimation()
    {
        int roll = guardAnimationRandom.Next(12);
        switch (roll)
        {
            case 0: SetGuardAnimationsPointGun(); break;
            case 1: SetGuardAnimationsMoveForward(); break;
            case 2: SetGuardAnimationsYawn(); break;
            case 3: SetGuardAnimationsStretch(); break;
            case 4: SetGuardAnimationsLongDistanceOverwatch(); break;
            case 5: SetGuardAnimationsTakeOffGoggles(); break;
            case 6: SetGuardAnimationsPatTheFloor(); break;
            case 7: SetGuardAnimationsPhaseInOut(); break;
            case 8: SetGuardAnimationsPeeWiggle(); break;
            case 9: SetGuardAnimationsLeanRight(); break;
            case 10: SetGuardAnimationsLeanLeft(); break;
            default: SetGuardAnimationsRollLeft(); break;
        }
    }

    private void StartRandomGuardAnimationEffect(EffectRequest request)
    {
        if (isTimedGuardAnimationActive)
        {
            Respond(request, EffectStatus.FailTemporary, StandardErrors.AlreadyInState, ["Guard animations", "already modified"]);
            return;
        }

        System.TimeSpan duration = TimeSpan.FromSeconds(request.Duration.TotalSeconds);
        int thisToken = ++guardAnimationToken;

        TryEffect(request,
            () => true,
            () =>
            {
                isTimedGuardAnimationActive = true;
                SetRandomGuardAnimation();
                _ = RunRandomGuardAnimations(thisToken, duration, TimeSpan.FromSeconds(5));
                return true;
            },
            () => Connector.SendMessage(text: $"{request.DisplayViewer} started random guard animations for {duration.TotalSeconds} seconds."));
    }

    private async Task RunRandomGuardAnimations(int token, TimeSpan duration, TimeSpan interval)
    {
        DateTime endTime = DateTime.UtcNow + duration;
        while (DateTime.UtcNow + interval < endTime)
        {
            await Task.Delay(interval);

            if (token != guardAnimationToken || !isTimedGuardAnimationActive)
            {
                return;
            }

            try
            {
                SetRandomGuardAnimation();
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to apply random guard animation: {ex.Message}");
            }
        }

        await ResetGuardAnimationsAfterDelay(token, endTime - DateTime.UtcNow);
    }

    private async Task ResetGuardAnimationsAfterDelay(int token, TimeSpan duration)
    {
        if (duration > TimeSpan.Zero)
        {
            await Task.Delay(duration);
        }

        // Only the most recent guard animation effect is allowed to reset to normal.
        if (token != guardAnimationToken)
        {
            return;
        }

        if (!isTimedGuardAnimationActive)
        {
            return;
        }

        try
        {
            SetGuardAnimationsNormal();
            isTimedGuardAnimationActive = false;
            Connector.SendMessage(text: "Guard animations have returned to normal.");
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to reset guard animations to normal: {ex.Message}");
        }
    }

    #endregion

    #region Alert Statuses

    private async void SetAlertStatus()
    {
        try
        {
            Log.Message("Attempting to set alert time to 9999");
            Set16(alertTimer, 9999);
            await Task.Delay(1000);
            Set16(alertTimer, 5000);
        }
        catch (Exception e)
        {
            Log.Error($"An error occurred while setting alert time: {e.Message}");
        }
    }

    private async void SetEvasionStatus()
    {
        try
        {
            Log.Message("Attempting to set alert time to 9999");
            Set16(alertTimer, 1100);
            await Task.Delay(500);
            Set16(alertTimer, 0);
            Set16(evasionTimer, 9999);
        }
        catch (Exception e)
        {
            Log.Error($"An error occurred while setting alert time: {e.Message}");
        }
    }

    private async void SetLongEvasionStatus()
    {
        try
        {
            Log.Message("Attempting to set alert time to 9999");
            Set16(alertTimer, 1100);
            await Task.Delay(500);
            Set16(alertTimer, 0);
            Set16(evasionTimer, 9999);
            await Task.Delay(2000);
            Set16(evasionTimer, 9999);
        }
        catch (Exception e)
        {
            Log.Error($"An error occurred while setting alert time: {e.Message}");
        }
    }

    // Caution is set by altering the alterStatusTrigger value to 2
    private async void SetCautionStatus()
    {
        try
        {
            Log.Message("Attempting to set alert status to Caution");
            Set16(alertStatusTrigger, 2);
        }
        catch (Exception e)
        {
            Log.Error($"An error occurred while setting alert status to Caution: {e.Message}");
        }
    }

    #endregion

    #region Camera and HUD

    private void SetLetterBoxMode()
    {
        try
        {
            Set8(letterBoxMode, 0);
        }
        catch (Exception e)
        {
            Log.Error($"An error occurred while setting letterbox mode: {e.Message}");
        }
    }

    private void UndoLetterBoxMode()
    {
        try
        {
            Set8(letterBoxMode, 1);
        }
        catch (Exception e)
        {
            Log.Error($"An error occurred while undoing letterbox mode: {e.Message}");
        }
    }

    private void SetDayMode()
    {
        try
        {
            Set8(dayOrNightMode, 255);
        }
        catch (Exception e)
        {
            Log.Error($"An error occurred while setting day or night mode: {e.Message}");
        }
    }

    private void SetNightMode()
    {
        try
        {
            Set8(dayOrNightMode, 0);
        }
        catch (Exception e)
        {
            Log.Error($"An error occurred while setting day or night mode: {e.Message}");
        }
    }

    private void SetCameraZoomOut()
    {
        try
        {
            Set8(cameraZoom, 1);
        }
        catch (Exception e)
        {
            Log.Error($"An error occurred while setting camera zoom: {e.Message}");
        }
    }

    private void SetCameraZoomNormal()
    {
        try
        {
            Set8(cameraZoom, 2);
        }
        catch (Exception e)
        {
            Log.Error($"An error occurred while setting camera zoom: {e.Message}");
        }
    }

    private void SetCameraZoomIn()
    {
        try
        {
            Set8(cameraZoom, 3);
        }
        catch (Exception e)
        {
            Log.Error($"An error occurred while setting camera zoom: {e.Message}");
        }
    }

    // Read the current camera zoom value
    private byte GetCameraZoom()
    {
        return Get8(cameraZoom);
    }

    private void SetMenuPauseDisabled()
    {
        try
        {
            SetArray(disableItemMenuPause, MenuPauseDisabledBytes);
            SetArray(disableWeaponMenuPause, MenuPauseDisabledBytes);
        }
        catch (Exception e)
        {
            Log.Error($"An error occurred while disabling item/weapon menu pause: {e.Message}");
        }
    }

    private void RestoreMenuPauseDefault()
    {
        try
        {
            if (disableItemMenuPause != null)
            {
                SetArray(disableItemMenuPause, ItemMenuPauseDefaultBytes);
            }

            if (disableWeaponMenuPause != null)
            {
                SetArray(disableWeaponMenuPause, WeaponMenuPauseDefaultBytes);
            }
        }
        catch (Exception e)
        {
            Log.Error($"An error occurred while restoring item/weapon menu pause: {e.Message}");
        }
    }

    #endregion

    #region Guards

    private void SetGuardAnimationsNormal()
    {
        byte[] normalBytes = { 0x0F, 0xBF, 0x90, 0x00, 0x0C, 0x00, 0x00 };
        SetArray(guardAnimations, normalBytes);
    }

    private void SetGuardAnimationsPointGun()
    {
        byte[] pointGunBytes = { 0xBA, 0x02, 0x00, 0x00, 0x00, 0x90, 0x90 };
        SetArray(guardAnimations, pointGunBytes);
    }

    private void SetGuardAnimationsMoveForward()
    {
        byte[] moveForwardBytes = { 0xBA, 0x03, 0x00, 0x00, 0x00, 0x90, 0x90 };
        SetArray(guardAnimations, moveForwardBytes);
    }

    private void SetGuardAnimationsYawn()
    {
        byte[] yawnBytes = { 0xBA, 0x04, 0x00, 0x00, 0x00, 0x90, 0x90 };
        SetArray(guardAnimations, yawnBytes);
    }

    private void SetGuardAnimationsStretch()
    {
        byte[] stretchBytes = { 0xBA, 0x05, 0x00, 0x00, 0x00, 0x90, 0x90 };
        SetArray(guardAnimations, stretchBytes);
    }

    private void SetGuardAnimationsSleepy()
    {
        byte[] sleepyBytes = { 0xBA, 0x06, 0x00, 0x00, 0x00, 0x90, 0x90 };
        SetArray(guardAnimations, sleepyBytes);
    }

    private void SetGuardAnimationsAttention()
    {
        byte[] attentionBytes = { 0xBA, 0x07, 0x00, 0x00, 0x00, 0x90, 0x90 };
        SetArray(guardAnimations, attentionBytes);
    }

    private void SetGuardAnimationsLongDistanceOverwatch()
    {
        byte[] longDistanceOverwatchBytes = { 0xBA, 0x0A, 0x00, 0x00, 0x00, 0x90, 0x90 };
        SetArray(guardAnimations, longDistanceOverwatchBytes);
    }

    private void SetGuardAnimationsTakeOffGoggles()
    {
        byte[] takeOffGogglesBytes = { 0xBA, 0x0B, 0x00, 0x00, 0x00, 0x90, 0x90 };
        SetArray(guardAnimations, takeOffGogglesBytes);
    }

    private void SetGuardAnimationsShortDistanceOverwatch()
    {
        byte[] shortDistanceOverwatchBytes = { 0xBA, 0x0D, 0x00, 0x00, 0x00, 0x90, 0x90 };
        SetArray(guardAnimations, shortDistanceOverwatchBytes);
    }

    private void SetGuardAnimationsPatTheFloor()
    {
        byte[] patTheFloorBytes = { 0xBA, 0x15, 0x00, 0x00, 0x00, 0x90, 0x90 };
        SetArray(guardAnimations, patTheFloorBytes);
    }

    private void SetGuardAnimationsPhaseInOut()
    {
        byte[] phaseInOutBytes = { 0xBA, 0x16, 0x00, 0x00, 0x00, 0x90, 0x90 };
        SetArray(guardAnimations, phaseInOutBytes);
    }

    private void SetGuardAnimationsPeeWiggle()
    {
        byte[] peeWiggleBytes = { 0xBA, 0x1B, 0x00, 0x00, 0x00, 0x90, 0x90 };
        SetArray(guardAnimations, peeWiggleBytes);
    }

    private void SetGuardAnimationsLeanRight()
    {
        byte[] leanRightBytes = { 0xBA, 0x1D, 0x00, 0x00, 0x00, 0x90, 0x90 };
        SetArray(guardAnimations, leanRightBytes);
    }

    private void SetGuardAnimationsLeanLeft()
    {
        byte[] leanLeftBytes = { 0xBA, 0x1E, 0x00, 0x00, 0x00, 0x90, 0x90 };
        SetArray(guardAnimations, leanLeftBytes);
    }

    private void SetGuardAnimationsRollLeft()
    {
        byte[] rollLeftBytes = { 0xBA, 0x1F, 0x00, 0x00, 0x00, 0x90, 0x90 };
        SetArray(guardAnimations, rollLeftBytes);
    }

    private void SetGuardSleepStatusNormal()
    {
        byte[] normalBytes = { 0x66, 0x44, 0x29, 0x82, 0x52, 0x13, 0x00, 0x00 };
        SetArray(guardSleepStatus, normalBytes);
    }

    private void SetGuardSleepStatusAlwaysAsleep()
    {
        byte[] alwaysAsleepBytes = { 0x83, 0xAA, 0x52, 0x13, 0x00, 0x00, 0x35, 0x90 };
        SetArray(guardSleepStatus, alwaysAsleepBytes);
    }

    private void SetGuardWakeStatusNormal()
    {
        byte[] normalBytes = { 0x66, 0x83, 0xAB, 0x5A, 0x13, 0x00, 0x00, 0x01, 0x79, 0x07 };
        SetArray(guardWakeStatus, normalBytes);
    }

    private void SetGuardWakeStatusAwake()
    {
        byte[] awakeBytes = { 0x66, 0x81, 0xAB, 0x5A, 0x13, 0x00, 0x00, 0x00, 0x10, 0x90 };
        SetArray(guardWakeStatus, awakeBytes);
    }

    #endregion

    #region Snake/Raiden Effects

    /* I would love to include this but need to find some check for when the player is in an animation
       to stop the flinch from locking the player in whatever animation they were in when it was triggered */
    private async void FlinchPlayer()
    {
        try
        {
            Log.Message("Attempting to flinch player");
            Set8(flinchPlayer, 1);
        }
        catch (Exception e)
        {
            Log.Error($"An error occurred while flinching player: {e.Message}");
        }
    }

    private bool EmptyClip()
    {
        try
        {
            Weapons equippedWep = GetEquippedWeaponEnum();
            if (equippedWep == Weapons.WEP_NONE || equippedWep == Weapons.WEP_DMIC || equippedWep == Weapons.WEP_HFBLADE || equippedWep == Weapons.WEP_COOLANT || equippedWep == Weapons.WEP_DMIC2 || equippedWep == Weapons.WEP_MAGAZINE || equippedWep == Weapons.WEP_BOOK || equippedWep == Weapons.WEP_GRENADE || equippedWep == Weapons.WEP_CLAYMORE || equippedWep == Weapons.WEP_C4 || equippedWep == Weapons.WEP_CHAFF || equippedWep == Weapons.WEP_STUNG || equippedWep == Weapons.WEP_NIKITA || equippedWep == Weapons.WEP_STINGER)
            {
                Log.Message("No valid weapon is currently equipped.");
                return false;
            }
            var manager = new WeaponItemManager(this);
            EmptyWeaponClip(0);
            Log.Message($"Emptied clip of {equippedWep}");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"An error occurred while emptying clip: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> BreakBox()
    {
        var manager = new WeaponItemManager(this);

        Items eqItem = GetEquippedItemEnum();

        if (eqItem != Items.ITM_BOX1 &&
            eqItem != Items.ITM_BOX2 &&
            eqItem != Items.ITM_BOX3 &&
            eqItem != Items.ITM_BOX4 &&
            eqItem != Items.ITM_BOX5 &&
            eqItem != Items.ITM_WETBOX)
        {
            Log.Message("No valid box is currently equipped.");
            return false;
        }

        short currentDurability = manager.ReadItemCapacity(eqItem);
        Log.Message($"Equipped box: {eqItem}, current durability = {currentDurability}");
        manager.WriteItemCapacity(eqItem, 1);
        await Task.Delay(50);
        manager.WriteItemCapacity(eqItem, 0);
        await Task.Delay(2000);

        manager.WriteItemCapacity(eqItem, currentDurability);
        Log.Message($"Restored {eqItem} durability to {currentDurability}.");

        return true;
    }



    #endregion

    #region Weapons

    private Weapons GetEquippedWeaponEnum()
    {
        short rawVal = Get16(equippedWeapon);
        Log.Message($"Equipped Weapon = {rawVal}");
        return (Weapons)rawVal;
    }

    private void EmptyWeaponClip(short newValue)
    {
        Set16(weaponClipCount, newValue);
    }

    private bool AddAmmo(Weapons weapon, short amountToAdd, out string message)
    {
        try
        {
            var manager = new WeaponItemManager(this);
            short currentAmmo = manager.ReadWeaponAmmo(weapon);

            // -1 / 65535 means the weapon is not currently owned; allow grant from zero.
            if (currentAmmo == -1)
            {
                currentAmmo = 0;
            }

            int newAmmoInt = Math.Clamp(currentAmmo + amountToAdd, 0, short.MaxValue);
            short newAmmo = (short)newAmmoInt;
            manager.WriteWeaponAmmo(weapon, newAmmo);

            message = $"Added {amountToAdd} ammo to {weapon}. Ammo: {currentAmmo} -> {newAmmo}";
            Log.Message(message);
            return true;
        }
        catch (Exception ex)
        {
            message = $"An error occurred while adding {amountToAdd} ammo to {weapon}: {ex.Message}";
            Log.Error(message);
            return false;
        }
    }

    private bool AddM9Ammo(short amountToAdd)
    {
        return AddAmmo(Weapons.WEP_M9, amountToAdd, out _);
    }

    private bool AddUspAmmo(short amountToAdd)
    {
        return AddAmmo(Weapons.WEP_USP, amountToAdd, out _);
    }

    private bool AddSocomAmmo(short amountToAdd)
    {
        return AddAmmo(Weapons.WEP_SOCOM, amountToAdd, out _);
    }

    private bool AddPsg1Ammo(short amountToAdd)
    {
        return AddAmmo(Weapons.WEP_PSG1, amountToAdd, out _);
    }

    private bool AddRgb6Ammo(short amountToAdd)
    {
        return AddAmmo(Weapons.WEP_RGB6, amountToAdd, out _);
    }

    private bool AddNikitaAmmo(short amountToAdd)
    {
        return AddAmmo(Weapons.WEP_NIKITA, amountToAdd, out _);
    }

    private bool AddStingerAmmo(short amountToAdd)
    {
        return AddAmmo(Weapons.WEP_STINGER, amountToAdd, out _);
    }

    private bool AddClaymoreAmmo(short amountToAdd)
    {
        return AddAmmo(Weapons.WEP_CLAYMORE, amountToAdd, out _);
    }

    private bool AddC4Ammo(short amountToAdd)
    {
        return AddAmmo(Weapons.WEP_C4, amountToAdd, out _);
    }

    private bool AddChaffAmmo(short amountToAdd)
    {
        return AddAmmo(Weapons.WEP_CHAFF, amountToAdd, out _);
    }

    private bool AddStungAmmo(short amountToAdd)
    {
        return AddAmmo(Weapons.WEP_STUNG, amountToAdd, out _);
    }

    private bool AddAks74uAmmo(short amountToAdd)
    {
        return AddAmmo(Weapons.WEP_AKS74U, amountToAdd, out _);
    }

    private bool AddMagazineAmmo(short amountToAdd)
    {
        return AddAmmo(Weapons.WEP_MAGAZINE, amountToAdd, out _);
    }

    private bool AddGrenadeAmmo(short amountToAdd)
    {
        return AddAmmo(Weapons.WEP_GRENADE, amountToAdd, out _);
    }

    private bool AddM4Ammo(short amountToAdd)
    {
        return AddAmmo(Weapons.WEP_M4, amountToAdd, out _);
    }

    private bool AddPsg1tAmmo(short amountToAdd)
    {
        return AddAmmo(Weapons.WEP_PSG1T, amountToAdd, out _);
    }

    private bool AddBookAmmo(short amountToAdd)
    {
        return AddAmmo(Weapons.WEP_BOOK, amountToAdd, out _);
    }

    private bool SubtractAmmo(Weapons weapon, short amountToSubtract, out string message)
    {
        try
        {
            var manager = new WeaponItemManager(this);
            short currentAmmo = manager.ReadWeaponAmmo(weapon);

            // Missing weapon reads as -1 / 65535; treat as zero and clamp write to zero.
            if (currentAmmo == -1)
            {
                currentAmmo = 0;
            }

            int newAmmoInt = Math.Clamp(currentAmmo - amountToSubtract, 0, short.MaxValue);
            short newAmmo = (short)newAmmoInt;
            manager.WriteWeaponAmmo(weapon, newAmmo);

            message = $"Subtracted {amountToSubtract} ammo from {weapon}. Ammo: {currentAmmo} -> {newAmmo}";
            Log.Message(message);
            return true;
        }
        catch (Exception ex)
        {
            message = $"An error occurred while subtracting {amountToSubtract} ammo from {weapon}: {ex.Message}";
            Log.Error(message);
            return false;
        }
    }

    private bool SubtractM9Ammo(short amountToSubtract)
    {
        return SubtractAmmo(Weapons.WEP_M9, amountToSubtract, out _);
    }

    private bool SubtractUspAmmo(short amountToSubtract)
    {
        return SubtractAmmo(Weapons.WEP_USP, amountToSubtract, out _);
    }

    private bool SubtractSocomAmmo(short amountToSubtract)
    {
        return SubtractAmmo(Weapons.WEP_SOCOM, amountToSubtract, out _);
    }

    private bool SubtractPsg1Ammo(short amountToSubtract)
    {
        return SubtractAmmo(Weapons.WEP_PSG1, amountToSubtract, out _);
    }

    private bool SubtractRgb6Ammo(short amountToSubtract)
    {
        return SubtractAmmo(Weapons.WEP_RGB6, amountToSubtract, out _);
    }

    private bool SubtractNikitaAmmo(short amountToSubtract)
    {
        return SubtractAmmo(Weapons.WEP_NIKITA, amountToSubtract, out _);
    }

    private bool SubtractStingerAmmo(short amountToSubtract)
    {
        return SubtractAmmo(Weapons.WEP_STINGER, amountToSubtract, out _);
    }

    private bool SubtractClaymoreAmmo(short amountToSubtract)
    {
        return SubtractAmmo(Weapons.WEP_CLAYMORE, amountToSubtract, out _);
    }

    private bool SubtractC4Ammo(short amountToSubtract)
    {
        return SubtractAmmo(Weapons.WEP_C4, amountToSubtract, out _);
    }

    private bool SubtractChaffAmmo(short amountToSubtract)
    {
        return SubtractAmmo(Weapons.WEP_CHAFF, amountToSubtract, out _);
    }

    private bool SubtractStungAmmo(short amountToSubtract)
    {
        return SubtractAmmo(Weapons.WEP_STUNG, amountToSubtract, out _);
    }

    private bool SubtractAks74uAmmo(short amountToSubtract)
    {
        return SubtractAmmo(Weapons.WEP_AKS74U, amountToSubtract, out _);
    }

    private bool SubtractMagazineAmmo(short amountToSubtract)
    {
        return SubtractAmmo(Weapons.WEP_MAGAZINE, amountToSubtract, out _);
    }

    private bool SubtractGrenadeAmmo(short amountToSubtract)
    {
        return SubtractAmmo(Weapons.WEP_GRENADE, amountToSubtract, out _);
    }

    private bool SubtractM4Ammo(short amountToSubtract)
    {
        return SubtractAmmo(Weapons.WEP_M4, amountToSubtract, out _);
    }

    private bool SubtractPsg1tAmmo(short amountToSubtract)
    {
        return SubtractAmmo(Weapons.WEP_PSG1T, amountToSubtract, out _);
    }

    private bool SubtractBookAmmo(short amountToSubtract)
    {
        return SubtractAmmo(Weapons.WEP_BOOK, amountToSubtract, out _);
    }

    private bool CanCharacterReceiveWeaponAmmo(string character, Weapons weapon, out string restrictionReason)
    {
        if ((character == "r_plt0" || character == "r_vr_b" || character == "r_vr_r") &&
            weapon == Weapons.WEP_USP)
        {
            restrictionReason = "cannot receive USP ammo";
            return false;
        }

        if (character == "r_tnk0" &&
            (weapon == Weapons.WEP_STINGER ||
             weapon == Weapons.WEP_SOCOM ||
             weapon == Weapons.WEP_RGB6 ||
             weapon == Weapons.WEP_PSG1T ||
             weapon == Weapons.WEP_PSG1 ||
             weapon == Weapons.WEP_NIKITA ||
             weapon == Weapons.WEP_M4 ||
             weapon == Weapons.WEP_CLAYMORE ||
             weapon == Weapons.WEP_C4 ||
             weapon == Weapons.WEP_BOOK ||
             weapon == Weapons.WEP_AKS74U))
        {
            restrictionReason = $"cannot receive {weapon} ammo";
            return false;
        }

        restrictionReason = string.Empty;
        return true;
    }

    private bool TryStartAddAmmoEffect(
        EffectRequest request,
        string[] codeParams,
        Weapons weapon,
        Func<short, bool> addAmmoAction,
        string successMessageTemplate)
    {
        if (codeParams.Length < 2)
        {
            Respond(request, EffectStatus.FailTemporary, StandardErrors.BadRequest);
            return false;
        }

        if (!int.TryParse(codeParams[1], out int quantity))
        {
            Respond(request, EffectStatus.FailTemporary, StandardErrors.CannotParseNumber, codeParams[1]);
            return false;
        }

        if (quantity <= 0 || quantity > short.MaxValue)
        {
            Respond(request, EffectStatus.FailTemporary, StandardErrors.BadRequest);
            return false;
        }

        currentCharacter = GetCharacterString();
        if (!CanCharacterReceiveWeaponAmmo(currentCharacter, weapon, out string restrictionReason))
        {
            Respond(request, EffectStatus.FailTemporary, StandardErrors.InvalidTarget, [currentCharacter, restrictionReason]);
            return false;
        }

        TryEffect(request,
            () => true,
            () => addAmmoAction((short)quantity),
            () => Connector.SendMessage(text: $"{request.DisplayViewer} {string.Format(successMessageTemplate, quantity)}"));
        return true;
    }

    private bool TryStartSubtractAmmoEffect(
        EffectRequest request,
        string[] codeParams,
        Weapons weapon,
        Func<short, bool> subtractAmmoAction,
        string successMessageTemplate)
    {
        if (codeParams.Length < 2)
        {
            Respond(request, EffectStatus.FailTemporary, StandardErrors.BadRequest);
            return false;
        }

        if (!int.TryParse(codeParams[1], out int quantity))
        {
            Respond(request, EffectStatus.FailTemporary, StandardErrors.CannotParseNumber, codeParams[1]);
            return false;
        }

        if (quantity <= 0 || quantity > short.MaxValue)
        {
            Respond(request, EffectStatus.FailTemporary, StandardErrors.BadRequest);
            return false;
        }

        currentCharacter = GetCharacterString();
        if (!CanCharacterReceiveWeaponAmmo(currentCharacter, weapon, out string restrictionReason))
        {
            Respond(request, EffectStatus.FailTemporary, StandardErrors.InvalidTarget, [currentCharacter, restrictionReason]);
            return false;
        }

        TryEffect(request,
            () => true,
            () => subtractAmmoAction((short)quantity),
            () => Connector.SendMessage(text: $"{request.DisplayViewer} {string.Format(successMessageTemplate, quantity)}"));
        return true;
    }

    #endregion

    #region Items

    private Items GetEquippedItemEnum()
    {
        short rawVal = Get16(equippedItem);
        Log.Message($"EquippedItem raw={rawVal}");
        return (Items)rawVal;
    }

    private bool SetItemAmount(Items item, short amountToSet, out string message)
    {
        try
        {
            var manager = new WeaponItemManager(this);
            manager.WriteItemCapacity(item, amountToSet);
            message = $"Set {item} amount to {amountToSet}.";
            Log.Message(message);
            return true;
        }
        catch (Exception ex)
        {
            message = $"An error occurred while setting {item} amount to {amountToSet}: {ex.Message}";
            Log.Error(message);
            return false;
        }
    }

    private bool AddStealth(short amountToSet)
    {
        return SetItemAmount(Items.ITM_STEALTH, amountToSet, out _);
    }

    private bool AddSensa(short amountToSet)
    {
        return SetItemAmount(Items.ITM_SENSA, amountToSet, out _);
    }

    private bool AddSensb(short amountToSet)
    {
        return SetItemAmount(Items.ITM_SENSB, amountToSet, out _);
    }

    private bool AddWetBox(short amountToSet)
    {
        return SetItemAmount(Items.ITM_WETBOX, amountToSet, out _);
    }

    private bool AddBox1(short amountToSet)
    {
        return SetItemAmount(Items.ITM_BOX1, amountToSet, out _);
    }

    private bool AddBox2(short amountToSet)
    {
        return SetItemAmount(Items.ITM_BOX2, amountToSet, out _);
    }

    private bool AddBox3(short amountToSet)
    {
        return SetItemAmount(Items.ITM_BOX3, amountToSet, out _);
    }

    private bool AddBox4(short amountToSet)
    {
        return SetItemAmount(Items.ITM_BOX4, amountToSet, out _);
    }

    private bool AddBox5(short amountToSet)
    {
        return SetItemAmount(Items.ITM_BOX5, amountToSet, out _);
    }

    private bool AddCigs(short amountToSet)
    {
        return SetItemAmount(Items.ITM_CIGS, amountToSet, out _);
    }

    private bool AddCard(short amountToSet)
    {
        return SetItemAmount(Items.ITM_CARD, amountToSet, out _);
    }

    private bool AddBandana(short amountToSet)
    {
        return SetItemAmount(Items.ITM_BANDANA, amountToSet, out _);
    }

    private bool AddSpwig(short amountToSet)
    {
        return SetItemAmount(Items.ITM_SPWIG, amountToSet, out _);
    }

    private bool AddWigA(short amountToSet)
    {
        return SetItemAmount(Items.ITM_WIGA, amountToSet, out _);
    }

    private bool AddWigB(short amountToSet)
    {
        return SetItemAmount(Items.ITM_WIGB, amountToSet, out _);
    }

    private bool AddWigC(short amountToSet)
    {
        return SetItemAmount(Items.ITM_WIGC, amountToSet, out _);
    }

    private bool AddWigD(short amountToSet)
    {
        return SetItemAmount(Items.ITM_WIGD, amountToSet, out _);
    }

    #endregion

    #endregion

    #region Crowd Control Effects

    public override EffectList Effects => new List<Effect>
    {
        // Alert Status
        new ("Set Alert Status", "setAlertStatus")
        {   Price = 80,
            Description = "Triggers an alert status, sending the guards to attack the player",
            Category = "Alert Statuses"
        },

        new ("Set Evasion Status", "setEvasionStatus")
        {   Price = 50,
            Description = "Triggers an evasion status, sending the guards to search for the player",
            Category = "Alert Statuses"
        },

        new ("Set Caution Status", "setCautionStatus")
        {   Price = 30,
            Description = "Triggers a caution status, keeping guards on high alert during patrols",
            Category = "Alert Statuses"
        },

        new ("Set Long Alert Status", "setLongAlertStatus")
        {   Price = 120,
            Duration = 20,
            Description = "Triggers an alert status for a longer duration",
            Category = "Alert Statuses"
        },

        new ("Set Long Evasion Status", "setLongEvasionStatus")
        {   Price = 60,
            Description = "Triggers an evasion status for a longer duration",
            Category = "Alert Statuses"
        },

        // Camera and HUD
        new ("Set HUD to Letterbox Mode", "setHudToLetterBoxMode")
        {   Price = 60,
            Duration = 30,
            Description = "Sets the game to letterbox mode",
            Category = "Camera and HUD"
        },

        new ("Make Camera Zoom in", "setCameraZoomIn")
        {   Price = 60,
            Duration = 30,
            Description = "Zooms the camera in",
            Category = "Camera and HUD"
        },

        new ("Set Game Lighting to Default", "setHudToDayMode")
        {   Price = 20,
            Description = "Sets the game to day mode, this effect is best used during the Plant chapter to notice it",
            Category = "Camera and HUD"
        },

        new ("Set Game Lighting to Night", "setHudToNightMode")
        {   Price = 20,
            Description = "Sets the game lighting to night mode, this effect is best used during the Plant chapter to notice it",
            Category = "Camera and HUD"
        },


        // Guards
        new ("Set Guard to Normal", "setGuardAnimationsNormal")
        {   Price = 30,
            Description = "Sets the guard animations to normal",
            Category = "Guards"
        },

        new ("Set Guard to Point Gun", "setGuardAnimationsPointGun")
        {   Price = 30,
            Duration = 15,
            Description = "Sets the guard animations to point gun during their patrol",
            Category = "Guards"
        },

        new ("Set Guard to Move Forward", "setGuardAnimationsMoveForward")
        {   Price = 30,
            Duration = 15,
            Description = "Sets the guard animations to move forward at their stopping points",
            Category = "Guards"
        },

        new ("Set Guard to Yawn", "setGuardAnimationsYawn")
        {   Price = 30,
            Duration = 15,
            Description = "Sets the guard animations to yawn when they stop during their patrol",
            Category = "Guards"
        },

        new ("Set Guard to Stretch", "setGuardAnimationsStretch")
        {   Price = 30,
            Duration = 15,
            Description = "Sets the guard animations to stretch when they stop during their patrol",
            Category = "Guards"
        },

        new ("Set Guard to Long-Distance Overwatch", "setGuardAnimationsLongDistanceOverwatch")
        {   Price = 30,
            Duration = 15,
            Description = "Sets the guard animations to look a long distance with their scope when they stop during their patrol",
            Category = "Guards"
        },

        new ("Set Guard to Take Off Goggles", "setGuardAnimationsTakeOffGoggles")
        {   Price = 30,
            Duration = 15,
            Description = "Sets the guard animations to take off their goggles when they stop during their patrol",
            Category = "Guards"
        },

        new ("Set Guard to Pat the Floor", "setGuardAnimationsPatTheFloor")
        {   Price = 30,
            Duration = 15,
            Description = "Sets the guard animations to pat the floor when they stop during their patrol",
            Category = "Guards"
        },

        new ("Set Guard to Phase In and Out", "setGuardAnimationsPhaseInOut")
        {   Price = 30,
            Duration = 15,
            Description = "Sets the guard animations to break the known laws of physics and phase in and out when they stop during their patrol",
            Category = "Guards"
        },

        new ("Set Guard to Pee Wiggle", "setGuardAnimationsPeeWiggle")
        {   Price = 30,
            Duration = 15,
            Description = "Sets the guard animations to wiggle as if they gotta pee when they stop during their patrol",
            Category = "Guards"
        },

        new ("Set Guard to Lean Right", "setGuardAnimationsLeanRight")
        {   Price = 30,
            Duration = 15,
            Description = "Sets the guard animations to lean right when they stop during their patrol",
            Category = "Guards"
        },

        new ("Set Guard to Lean Left", "setGuardAnimationsLeanLeft")
        {   Price = 30,
            Duration = 15,
            Description = "Sets the guard animations to lean left when they stop during their patrol",
            Category = "Guards"
        },

        new ("Set Guard to Roll Left", "setGuardAnimationsRollLeft")
        {   Price = 30,
            Duration = 15,
            Description = "Sets the guard animations to side roll to the left when they stop during their patrol",
            Category = "Guards"
        },

        new ("Random Guard Animations", "setGuardAnimationsRandom")
        {   Price = 60,
            Duration = 30,
            Description = "Randomly changes guard animations every 5 seconds, then returns to normal",
            Category = "Guards"
        },

        new ("All Guards go to Sleep", "forceGuardsToSleep")
        {   Price = 60,
            Description = "Forces all guards to sleep",
            Category = "Guards"
        },

        new ("All Guards Wake Up", "forceGuardsToWakeUp")
        {   Price = 60,
            Description = "Forces all guards asleep to wake up",
            Category = "Guards"
        },

        new ("All Guards Can't Be Knocked Out", "guardCantBeKnockedOut")
        {   Price = 120,
            Duration = 30,
            Description = "Makes guards immune to being knocked out for a short period",
            Category = "Guards"
        },

        // Snake/Raiden
        new ("Empty Gun Clip", "emptyGunClip")
        {
            Price = 60,
            Duration = 5,
            Description = "Continuously empties the player's weapon clip for a short duration",
            Category = "Snake/Raiden"
        },

        new ("Infinite Ammo", "infiniteAmmo")
        {
            Price = 150,
            Duration = 20,
            Description = "Gives the player infinite ammo for a short duration",
            Category = "Snake/Raiden"
        },

        new ("Disable Item/Weapon Menu Pause", "disableMenuPause")
        {
            Price = 120,
            Duration = 60,
            Description = "Disables item and weapon menu pause so gameplay continues while the menu is open",
            Category = "Snake/Raiden"
        },

        new ("Break Box", "breakBox")
        {
            Price = 80,
            Description = "Breaks the player's currently equipped box.... Snake won't be happy about this one",
            Category = "Snake/Raiden"
        },

        /* Commented out until I can find a way to check if the player is in an animation to prevent the player from being locked in an animation 
        new ("Flinch Player", "flinchPlayer")
        {   Price = 60,
            Description = "Makes the player flinch",
            Category = "Snake/Raiden"
        },
        */

        // Ammo (Add)

        new ("+ M9 Ammo", "addM9Ammo")
        {   Price = 2,
            Quantity = 100,
            Description = "Adds ammo to the M9",
            Category = "Ammo (Add)",
            Image = "give_ammo"
        },

        new ("+ USP Ammo", "addUspAmmo")
        {   Price = 2,
            Quantity = 100,
            Description = "Adds ammo to the USP",
            Category = "Ammo (Add)",
            Image = "give_ammo"
        },

        new ("+ SOCOM Ammo", "addSocomAmmo")
        {   Price = 2,
            Quantity = 100,
            Description = "Adds ammo to the SOCOM",
            Category = "Ammo (Add)",
            Image = "give_ammo"
        },

        new ("+ PSG1 Ammo", "addPsg1Ammo")
        { Price = 2,
            Quantity = 100,
            Description = "Adds ammo to the PSG1",
            Category = "Ammo (Add)",
            Image = "give_ammo"
        },

        new ("+ RGB6 Ammo", "addRgb6Ammo")
        {   Price = 2,
            Quantity = 100,
            Description = "Adds ammo to the RGB6",
            Category = "Ammo (Add)",
            Image = "give_ammo"
        },

        new ("+ Nikita Ammo", "addNikitaAmmo")
        {   Price = 2,
            Quantity = 100,
            Description = "Adds ammo to the Nikita",
            Category = "Ammo (Add)",
            Image = "give_ammo"
        },

        new ("+ Stinger Ammo", "addStingerAmmo")
        {   Price = 2,
            Quantity = 100,
            Description = "Adds ammo to the Stinger",
            Category = "Ammo (Add)",
            Image = "give_ammo"
        },

        new ("+ Claymores", "addClaymoreAmmo")
        {   Price = 2,
            Quantity = 100,
            Description = "Adds ammo to the Claymore",
            Category = "Ammo (Add)",
            Image = "give_ammo"
        },

        new ("+ C4", "addC4Ammo")
        {   Price = 2,
            Quantity = 100,
            Description = "Adds ammo to the C4",
            Category = "Ammo (Add)",
            Image = "give_ammo"
        },

        new ("+ Chaff Grenades", "addChaffAmmo")
        {   Price = 2,
            Quantity = 100,
            Description = "Adds ammo to the Chaff",
            Category = "Ammo (Add)",
            Image = "give_ammo"
        },

        new ("+ Stun Grenades", "addStungAmmo")
        {   Price = 2,
            Quantity = 100,
            Description = "Adds ammo to the Stung",
            Category = "Ammo (Add)",
            Image = "give_ammo"
        },

        new ("+ AKS74U Ammo", "addAks74uAmmo")
        {   Price = 2,
            Quantity = 100,
            Description = "Adds ammo to the AKS74U",
            Category = "Ammo (Add)",
            Image = "give_ammo"
        },

        new ("+ Magazine Ammo", "addMagazineAmmo")
        {   Price = 2,
            Quantity = 100,
            Description = "Gives the player extra Empty Magazines to throw",
            Category = "Ammo (Add)",
            Image = "give_ammo"
        },

        new ("+ Grenades", "addGrenadeAmmo")
        {   Price = 2,
            Quantity = 100,
            Description = "Adds to the Grenade ",
            Category = "Ammo (Add)",
            Image = "give_ammo"
        },

        new ("+ M4 Ammo", "addM4Ammo")
        {   Price = 2,
            Quantity = 100,
            Description = "Adds ammo to the M4",
            Category = "Ammo (Add)",
            Image = "give_ammo"
        },

        new ("+ PSG1-T Ammo", "addPsg1tAmmo")
        {   Price = 2,
            Quantity = 100,
            Description = "Adds ammo to the PSG1-T",
            Category = "Ammo (Add)",
            Image = "give_ammo"
        },

        new ("+ Books", "addBookAmmo")
        {   Price = 2,
            Quantity = 100,
            Description = "Adds ammo to the Book",
            Category = "Ammo (Add)",
            Image = "give_ammo"
        },

        // Ammo (Subtract)
        new("- M9 Ammo", "subtractM9Ammo")
        {   Price = 2,
            Quantity = 100,
            Description = "Subtracts ammo from the M9",
            Category = "Ammo (Subtract)"
        },

        new("- USP Ammo", "subtractUspAmmo")
        {   Price = 2,
            Quantity = 100,
            Description = "Subtracts ammo from the USP",
            Category = "Ammo (Subtract)"
        },

        new("- SOCOM Ammo", "subtractSocomAmmo")
        {   Price = 2,
            Quantity = 100,
            Description = "Subtracts ammo from the SOCOM",
            Category = "Ammo (Subtract)"
        },

        new("- PSG1 Ammo", "subtractPsg1Ammo")
        {   Price = 2,
            Quantity = 100,
            Description = "Subtracts ammo from the PSG1",
            Category = "Ammo (Subtract)"
        },

        new("- RGB6 Ammo", "subtractRgb6Ammo")
        {   Price = 2,
            Quantity = 100,
            Description = "Subtracts ammo from the RGB6",
            Category = "Ammo (Subtract)"
        },

        new("- Nikita Ammo", "subtractNikitaAmmo")
        {   Price = 2,
            Quantity = 100,
            Description = "Subtracts ammo from the Nikita",
            Category = "Ammo (Subtract)"
        },

        new("- Stinger Ammo", "subtractStingerAmmo")
        {   Price = 2,
            Quantity = 100,
            Description = "Subtracts ammo from the Stinger",
            Category = "Ammo (Subtract)"
        },

        new("- Claymores", "subtractClaymoreAmmo")
        {   Price = 2,
            Quantity = 100,
            Description = "Subtracts ammo from the Claymore",
            Category = "Ammo (Subtract)"
        },

        new("- C4", "subtractC4Ammo")
        {   Price = 2,
            Quantity = 100,
            Description = "Subtracts ammo from the C4",
            Category = "Ammo (Subtract)"
        },

        new("- Chaff Grenades", "subtractChaffAmmo")
        {   Price = 2,
            Quantity = 100,
            Description = "Subtracts ammo from the Chaff",
            Category = "Ammo (Subtract)"
        },

        new("- Stun Grenades", "subtractStungAmmo")
        {   Price = 2,
            Quantity = 100,
            Description = "Subtracts ammo from the Stung",
            Category = "Ammo (Subtract)"
        },

        new("- AKS74U Ammo", "subtractAks74uAmmo")
        {   Price = 2,
            Quantity = 100,
            Description = "Subtracts ammo from the AKS74U",
            Category = "Ammo (Subtract)"
        },

        new("- Magazine Ammo", "subtractMagazineAmmo")
        {   Price = 2,
            Quantity = 100,
            Description = "Subtracts Empty Magazine count",
            Category = "Ammo (Subtract)"
        },

        new("- Grenades", "subtractGrenadeAmmo")
        {   Price = 2,
            Quantity = 100,
            Description = "Subtracts from the Grenade pouch",
            Category = "Ammo (Subtract)"
        },

        new("- M4 Ammo", "subtractM4Ammo")
        {   Price = 2,
            Quantity = 100,
            Description = "Subtracts ammo from the M4",
            Category = "Ammo (Subtract)"
        },

        new("- PSG1-T Ammo", "subtractPsg1tAmmo")
        {   Price = 2,
            Quantity = 100,
            Description = "Subtracts ammo from the PSG1-T",
            Category = "Ammo (Subtract)"
        },

        new("- Books", "subtractBookAmmo")
        {   Price = 2,
            Quantity = 100,
            Description = "Subtracts ammo from the Book",
            Category = "Ammo (Subtract)"
        },

    };

    protected override void StartEffect(EffectRequest request)
    {
        var codeParams = FinalCode(request: request).Split(separator: '_');
        switch (codeParams[0])
        {
            #region Alert Statuses

            case "setAlertStatus":
                TryEffect(request,
                    () => true,
                    () =>
                    {
                        SetAlertStatus();
                        return true;
                    },
                    () => Connector.SendMessage(text: $"{request.DisplayViewer} set the game to Alert Status."));
                break;

            case "setEvasionStatus":
                TryEffect(request,
                    () => true,
                    () =>
                    {
                        SetEvasionStatus();
                        return true;
                    },
                    () => Connector.SendMessage(text: $"{request.DisplayViewer} set the game to Evasion Status."));
                break;

            case "setCautionStatus":
                TryEffect(request,
                    () => true,
                    () =>
                    {
                        SetCautionStatus();
                        return true;
                    },
                    () => Connector.SendMessage(text: $"{request.DisplayViewer} set the game to Caution Status."));
                break;

            case "setLongAlertStatus":
                var longAlertDuration = request.Duration = TimeSpan.FromSeconds(value: 20);
                var longAlertAct = RepeatAction(request,
                    () => true,
                    () => Connector.SendMessage(text: $"{request.DisplayViewer} set the game to Alert Status for {longAlertDuration.TotalSeconds} seconds."),
                    TimeSpan.Zero,
                    () => IsReady(request: request),
                    TimeSpan.FromMilliseconds(value: 500),
                    () =>
                    {
                        Set16(alertTimer, 9999);
                        return true;
                    },
                    TimeSpan.FromMilliseconds(value: 500),
                    false);
                longAlertAct.WhenCompleted.Then
                    (f: _ =>
                    {
                        Connector.SendMessage(text: "Alert Status has ended.");
                    });
                break;

            case "setLongEvasionStatus":
                TryEffect(request,
                    () => true,
                    () =>
                    {
                        SetLongEvasionStatus();
                        return true;
                    },
                    () => Connector.SendMessage(text: $"{request.DisplayViewer} set the game to Evasion Status."));
                break;

            #endregion

            #region Camera and HUD

            case "setHudToLetterBoxMode":
                var letterBoxDuration = request.Duration = TimeSpan.FromSeconds(value: 30);
                var letterBoxAct = RepeatAction(request,
                    () => true,
                    () => Connector.SendMessage(text: $"{request.DisplayViewer} set the game to letterbox mode for {letterBoxDuration.TotalSeconds} seconds."),
                    TimeSpan.Zero,
                    () => IsReady(request: request),
                    TimeSpan.FromMilliseconds(value: 500),
                    () =>
                    {
                        SetLetterBoxMode();
                        return true;
                    },
                    TimeSpan.FromMilliseconds(value: 500),
                    false);
                letterBoxAct.WhenCompleted.Then
                    (f: _ =>
                    {
                        UndoLetterBoxMode();
                        Connector.SendMessage(text: "Letterbox mode has ended.");
                    });
                break;

            case "setCameraZoomIn":
                byte current1 = GetCameraZoom();
                if (current1 == 3)
                {
                    Respond(request, EffectStatus.FailTemporary, StandardErrors.AlreadyInState, ["Camera", "zoomed-in"]);
                    break;
                }

                var zoomInDuration = request.Duration = TimeSpan.FromSeconds(value: 30);
                var zoomInAct = RepeatAction(request,
                    () => true,
                    () => Connector.SendMessage(text: $"{request.DisplayViewer} zoomed the camera in for {zoomInDuration.TotalSeconds} seconds."),
                    TimeSpan.Zero,
                    () => IsReady(request: request),
                    TimeSpan.FromMilliseconds(value: 500),
                    () =>
                    {
                        SetCameraZoomIn();
                        return true;
                    },
                    TimeSpan.FromMilliseconds(value: 500),
                    false);
                zoomInAct.WhenCompleted.Then
                    (f: _ =>
                    {
                        SetCameraZoomNormal();
                        Connector.SendMessage(text: "Camera zoom in has ended.");
                    });
                break;

            case "setHudToDayMode":
                TryEffect(request,
                    () => true,
                    () =>
                    {
                        SetDayMode();
                        return true;
                    },
                    () => Connector.SendMessage(text: $"{request.DisplayViewer} set the game to day mode."));
                break;

            case "setHudToNightMode":
                TryEffect(request,
                    () => true,
                    () =>
                    {
                        SetNightMode();
                        return true;
                    },
                    () => Connector.SendMessage(text: $"{request.DisplayViewer} set the game to night mode."));
                break;

            #endregion

            #region Guards

            case "setGuardAnimationsNormal":
                ++guardAnimationToken;
                isTimedGuardAnimationActive = false;
                TryEffect(request,
                    () => true,
                    () =>
                    {
                        SetGuardAnimationsNormal();
                        return true;
                    },
                    () => Connector.SendMessage(text: $"{request.DisplayViewer} set the guard animations to normal."));
                break;

            case "setGuardAnimationsPointGun":
                StartTimedGuardAnimationEffect(request, SetGuardAnimationsPointGun, "point gun");
                break;

            case "setGuardAnimationsMoveForward":
                StartTimedGuardAnimationEffect(request, SetGuardAnimationsMoveForward, "move forward");
                break;

            case "setGuardAnimationsYawn":
                StartTimedGuardAnimationEffect(request, SetGuardAnimationsYawn, "yawn");
                break;

            case "setGuardAnimationsStretch":
                StartTimedGuardAnimationEffect(request, SetGuardAnimationsStretch, "stretch");
                break;

            case "setGuardAnimationsLongDistanceOverwatch":
                StartTimedGuardAnimationEffect(request, SetGuardAnimationsLongDistanceOverwatch, "long-distance overwatch");
                break;

            case "setGuardAnimationsTakeOffGoggles":
                StartTimedGuardAnimationEffect(request, SetGuardAnimationsTakeOffGoggles, "take off goggles");
                break;

            case "setGuardAnimationsPatTheFloor":
                StartTimedGuardAnimationEffect(request, SetGuardAnimationsPatTheFloor, "pat the floor");
                break;

            case "setGuardAnimationsPhaseInOut":
                StartTimedGuardAnimationEffect(request, SetGuardAnimationsPhaseInOut, "phase in and out");
                break;

            case "setGuardAnimationsPeeWiggle":
                StartTimedGuardAnimationEffect(request, SetGuardAnimationsPeeWiggle, "pee wiggle");
                break;

            case "setGuardAnimationsLeanRight":
                StartTimedGuardAnimationEffect(request, SetGuardAnimationsLeanRight, "lean right");
                break;

            case "setGuardAnimationsLeanLeft":
                StartTimedGuardAnimationEffect(request, SetGuardAnimationsLeanLeft, "lean left");
                break;

            case "setGuardAnimationsRollLeft":
                StartTimedGuardAnimationEffect(request, SetGuardAnimationsRollLeft, "roll left");
                break;

            case "setGuardAnimationsRandom":
                StartRandomGuardAnimationEffect(request);
                break;

            case "forceGuardsToSleep":
                TryEffect(request,
                    () => true,
                    () =>
                    {
                        SetGuardSleepStatusAlwaysAsleep();
                        // Waiting 5 seconds gives it time to sleep all awake guards before returning to normal
                        Task.Delay(millisecondsDelay: 5000).ContinueWith(continuationAction: _ => SetGuardSleepStatusNormal());
                        return true;
                    },
                    () => Connector.SendMessage(text: $"{request.DisplayViewer} forced all guards to sleep."));
                break;

            case "forceGuardsToWakeUp":
                TryEffect(request,
                    () => true,
                    () =>
                    {
                        SetGuardWakeStatusAwake();
                        // Waiting 5 seconds gives it time to wake all sleeping guards before returning to normal
                        Task.Delay(millisecondsDelay: 5000).ContinueWith(continuationAction: _ => SetGuardWakeStatusNormal());
                        return true;
                    },
                    () => Connector.SendMessage(text: $"{request.DisplayViewer} forced all guards to wake up."));
                break;

            case "guardCantBeKnockedOut":
                var guardCantBeKnockedOutDuration = request.Duration = TimeSpan.FromSeconds(value: 30);
                var guardCantBeKnockedOutAct = RepeatAction(request,
                    () => true,
                    () => Connector.SendMessage(text: $"{request.DisplayViewer} made all guards immune to being knocked out for {guardCantBeKnockedOutDuration.TotalSeconds} seconds."),
                    TimeSpan.Zero,
                    () => IsReady(request: request),
                    TimeSpan.FromMilliseconds(value: 500),
                () =>
                {
                    SetGuardWakeStatusAwake();
                    return true;
                },
                    TimeSpan.FromMilliseconds(value: 500),
                false);
                guardCantBeKnockedOutAct.WhenCompleted.Then
                    (f: _ =>
                    {
                        SetGuardWakeStatusNormal();
                        Connector.SendMessage(text: "Guard knock out immunity has ended.");
                    });
                break;

            #endregion

            #region Snake/Raiden
            /* Commented out until I can find a way to check if the player is in an animation to prevent the player from being locked in an animation
            case "flinchPlayer":
                    TryEffect(request,
                        () => true,
                        () =>
                        {
                            FlinchPlayer();
                            return true;
                        },
                        () => Connector.SendMessage($"{request.DisplayViewer} made the player flinch."),
                        null, true);
                    break;
            */

            case "emptyGunClip":
                var emptyGunClipDuration = request.Duration = TimeSpan.FromSeconds(value: 5);
                var emptyGunClipAct = RepeatAction(request,
                    () => true,
                    () => Connector.SendMessage(text: $"{request.DisplayViewer} emptied the player's gun clip for {emptyGunClipDuration.TotalSeconds} seconds."),
                    TimeSpan.Zero,
                    () => IsReady(request: request),
                    TimeSpan.FromMilliseconds(value: 500),
                () =>
                {
                    EmptyClip();
                    return true;
                },
                    TimeSpan.FromMilliseconds(value: 500),
                false);
                emptyGunClipAct.WhenCompleted.Then
                    (f: _ =>
                    {
                        Connector.SendMessage(text: "Gun clip has been refilled.");
                    });
                break;


            case "infiniteAmmo":
                currentCharacter = GetCharacterString();
                // Bandana for Snake
                if (currentCharacter == "r_tnk0" || currentCharacter == "r_vr_s" || currentCharacter == "r_vr_1")
                {
                    var infiniteAmmoDuration = request.Duration = TimeSpan.FromSeconds(value: 20);
                    var infiniteAmmoAct = RepeatAction(request,
                        () => true,
                        () => Connector.SendMessage(text: $"{request.DisplayViewer} gave the player the Bandana for {infiniteAmmoDuration.TotalSeconds} seconds."),
                        TimeSpan.Zero,
                        () => IsReady(request: request),
                        TimeSpan.FromMilliseconds(value: 500),
                    () =>
                    {
                        AddBandana(amountToSet: 1);
                        Set16(equippedItem, (short)Items.ITM_BANDANA);
                        return true;
                    },
                        TimeSpan.FromMilliseconds(value: 500),
                    false);
                    infiniteAmmoAct.WhenCompleted.Then
                        (f: _ =>
                        {
                            AddBandana(amountToSet: 0);
                            Connector.SendMessage(text: "Bandana effect has ended.");
                        });
                }

                // SP_WIG for Raiden
                else if (currentCharacter == "r_plt0" || currentCharacter == "r_vr_b" || currentCharacter == "r_vr_r")
                {
                    var infiniteAmmoDuration = request.Duration = TimeSpan.FromSeconds(value: 20);
                    var infiniteAmmoAct = RepeatAction(request,
                        () => true,
                        () => Connector.SendMessage(text: $"{request.DisplayViewer} gave the player the SPWIG for {infiniteAmmoDuration.TotalSeconds} seconds."),
                        TimeSpan.Zero,
                        () => IsReady(request: request),
                        TimeSpan.FromMilliseconds(value: 500),
                    () =>
                    {
                        AddSpwig(amountToSet: 1);
                        Set16(equippedItem, (short)Items.ITM_SPWIG);
                        return true;
                    },
                        TimeSpan.FromMilliseconds(value: 500),
                    false);
                    infiniteAmmoAct.WhenCompleted.Then
                        (f: _ =>
                        {
                            AddSpwig(amountToSet: 0);
                            Connector.SendMessage(text: "SPWIG effect has ended.");
                        });
                }
                else
                {
                    Respond(request, EffectStatus.FailTemporary, StandardErrors.InvalidTarget, ["This character", "infinite ammo"]);
                }
                break;

            case "disableMenuPause":
                var disableMenuPauseDuration = TimeSpan.FromSeconds(request.Duration.TotalSeconds);
                var disableMenuPauseAct = RepeatAction(request,
                    () => true,
                    () => Connector.SendMessage(text: $"{request.DisplayViewer} disabled item and weapon menu pause for {disableMenuPauseDuration.TotalSeconds} seconds."),
                    TimeSpan.Zero,
                    () => IsReady(request: request),
                    TimeSpan.FromMilliseconds(value: 500),
                () =>
                {
                    SetMenuPauseDisabled();
                    return true;
                },
                    TimeSpan.FromMilliseconds(value: 500),
                false);
                disableMenuPauseAct.WhenCompleted.Then
                    (f: _ =>
                    {
                        RestoreMenuPauseDefault();
                        Connector.SendMessage(text: "Item and weapon menu pause behavior has been restored.");
                    });
                break;

            case "breakBox":
                Items eq = GetEquippedItemEnum();
                if (eq != Items.ITM_BOX1 &&
                    eq != Items.ITM_BOX2 &&
                    eq != Items.ITM_BOX3 &&
                    eq != Items.ITM_BOX4 &&
                    eq != Items.ITM_BOX5 &&
                    eq != Items.ITM_WETBOX)
                {
                    Respond(request, EffectStatus.FailTemporary, StandardErrors.PrerequisiteNotFound, "A box");
                    break;
                }
                TryEffect(request,
                    () => true,
                    () =>
                    {
                        BreakBox();
                        return true;
                    },
                    () => Connector.SendMessage(text: $"{request.DisplayViewer} broke the player's box."));
                break;

            #endregion

            #region Ammo

            case "subtractM9Ammo":
                {
                    TryStartSubtractAmmoEffect(request, codeParams, Weapons.WEP_M9, SubtractM9Ammo, "subtracted {0} ammo from the M9.");
                    break;
                }

            case "subtractUspAmmo":
                {
                    TryStartSubtractAmmoEffect(request, codeParams, Weapons.WEP_USP, SubtractUspAmmo, "subtracted {0} ammo from the USP.");
                    break;
                }

            case "subtractSocomAmmo":
                {
                    TryStartSubtractAmmoEffect(request, codeParams, Weapons.WEP_SOCOM, SubtractSocomAmmo, "subtracted {0} ammo from the SOCOM.");
                    break;
                }

            case "subtractPsg1Ammo":
                {
                    TryStartSubtractAmmoEffect(request, codeParams, Weapons.WEP_PSG1, SubtractPsg1Ammo, "subtracted {0} ammo from the PSG1.");
                    break;
                }

            case "subtractRgb6Ammo":
                {
                    TryStartSubtractAmmoEffect(request, codeParams, Weapons.WEP_RGB6, SubtractRgb6Ammo, "subtracted {0} grenades from the RGB6.");
                    break;
                }

            case "subtractNikitaAmmo":
                {
                    TryStartSubtractAmmoEffect(request, codeParams, Weapons.WEP_NIKITA, SubtractNikitaAmmo, "subtracted {0} remote controlled missiles from the Nikita.");
                    break;
                }

            case "subtractStingerAmmo":
                {
                    TryStartSubtractAmmoEffect(request, codeParams, Weapons.WEP_STINGER, SubtractStingerAmmo, "subtracted {0} missiles from the Stinger.");
                    break;
                }

            case "subtractClaymoreAmmo":
                {
                    TryStartSubtractAmmoEffect(request, codeParams, Weapons.WEP_CLAYMORE, SubtractClaymoreAmmo, "subtracted {0} from the Claymore pouch.");
                    break;
                }

            case "subtractC4Ammo":
                {
                    TryStartSubtractAmmoEffect(request, codeParams, Weapons.WEP_C4, SubtractC4Ammo, "subtracted {0} from the C4 count.");
                    break;
                }

            case "subtractChaffAmmo":
                {
                    TryStartSubtractAmmoEffect(request, codeParams, Weapons.WEP_CHAFF, SubtractChaffAmmo, "subtracted {0} from the Chaff Grenade pouch.");
                    break;
                }

            case "subtractStungAmmo":
                {
                    TryStartSubtractAmmoEffect(request, codeParams, Weapons.WEP_STUNG, SubtractStungAmmo, "subtracted {0} from the Stun Grenade pouch.");
                    break;
                }

            case "subtractAks74uAmmo":
                {
                    TryStartSubtractAmmoEffect(request, codeParams, Weapons.WEP_AKS74U, SubtractAks74uAmmo, "subtracted {0} ammo from the AKS74U.");
                    break;
                }

            case "subtractMagazineAmmo":
                {
                    TryStartSubtractAmmoEffect(request, codeParams, Weapons.WEP_MAGAZINE, SubtractMagazineAmmo, "subtracted {0} from the Empty Magazine count.");
                    break;
                }

            case "subtractGrenadeAmmo":
                {
                    TryStartSubtractAmmoEffect(request, codeParams, Weapons.WEP_GRENADE, SubtractGrenadeAmmo, "subtracted {0} from the Grenade pouch.");
                    break;
                }

            case "subtractM4Ammo":
                {
                    TryStartSubtractAmmoEffect(request, codeParams, Weapons.WEP_M4, SubtractM4Ammo, "subtracted {0} ammo from the M4.");
                    break;
                }

            case "subtractPsg1tAmmo":
                {
                    TryStartSubtractAmmoEffect(request, codeParams, Weapons.WEP_PSG1T, SubtractPsg1tAmmo, "subtracted {0} ammo from the PSG1-T.");
                    break;
                }

            case "subtractBookAmmo":
                {
                    TryStartSubtractAmmoEffect(request, codeParams, Weapons.WEP_BOOK, SubtractBookAmmo, "subtracted {0} from the Book supply.");
                    break;
                }

            case "addM9Ammo":
                {
                    TryStartAddAmmoEffect(request, codeParams, Weapons.WEP_M9, AddM9Ammo, "added {0} ammo to the M9.");
                    break;
                }

            case "addUspAmmo":
                {
                    TryStartAddAmmoEffect(request, codeParams, Weapons.WEP_USP, AddUspAmmo, "added {0} ammo to the USP.");
                    break;
                }

            case "addSocomAmmo":
                {
                    TryStartAddAmmoEffect(request, codeParams, Weapons.WEP_SOCOM, AddSocomAmmo, "added {0} ammo to the SOCOM.");
                    break;
                }

            case "addPsg1Ammo":
                {
                    TryStartAddAmmoEffect(request, codeParams, Weapons.WEP_PSG1, AddPsg1Ammo, "added {0} ammo to the PSG1.");
                    break;
                }

            case "addRgb6Ammo":
                {
                    TryStartAddAmmoEffect(request, codeParams, Weapons.WEP_RGB6, AddRgb6Ammo, "added {0} grenades into the RGB6.");
                    break;
                }



            case "addNikitaAmmo":
                {
                    TryStartAddAmmoEffect(request, codeParams, Weapons.WEP_NIKITA, AddNikitaAmmo, "added {0} remote controlled missiles to the Nikita.");
                    break;
                }

            case "addStingerAmmo":
                {
                    TryStartAddAmmoEffect(request, codeParams, Weapons.WEP_STINGER, AddStingerAmmo, "added {0} missiles to the Stinger.");
                    break;
                }

            case "addClaymoreAmmo":
                {
                    TryStartAddAmmoEffect(request, codeParams, Weapons.WEP_CLAYMORE, AddClaymoreAmmo, "added {0} to the Claymore pouch.");
                    break;
                }

            case "addC4Ammo":
                {
                    TryStartAddAmmoEffect(request, codeParams, Weapons.WEP_C4, AddC4Ammo, "added {0} to the C4 count.");
                    break;
                }

            case "addChaffAmmo":
                {
                    TryStartAddAmmoEffect(request, codeParams, Weapons.WEP_CHAFF, AddChaffAmmo, "added {0} to the Chaff Grenade pouch.");
                    break;
                }

            case "addStungAmmo":
                {
                    TryStartAddAmmoEffect(request, codeParams, Weapons.WEP_STUNG, AddStungAmmo, "added {0} to the Stun Grenade pouch.");
                    break;
                }

            case "addAks74uAmmo":
                {
                    TryStartAddAmmoEffect(request, codeParams, Weapons.WEP_AKS74U, AddAks74uAmmo, "added {0} ammo to the AKS74U.");
                    break;
                }

            case "addMagazineAmmo":
                {
                    TryStartAddAmmoEffect(request, codeParams, Weapons.WEP_MAGAZINE, AddMagazineAmmo, "added {0} to the Empty Magazine count.");
                    break;
                }

            case "addGrenadeAmmo":
                {
                    TryStartAddAmmoEffect(request, codeParams, Weapons.WEP_GRENADE, AddGrenadeAmmo, "added {0} to the Grenade pouch.");
                    break;
                }

            case "addM4Ammo":
                {
                    TryStartAddAmmoEffect(request, codeParams, Weapons.WEP_M4, AddM4Ammo, "added {0} ammo to the M4.");
                    break;
                }

            case "addPsg1tAmmo":
                {
                    TryStartAddAmmoEffect(request, codeParams, Weapons.WEP_PSG1T, AddPsg1tAmmo, "added {0} ammo to the PSG1-T.");
                    break;
                }

            case "addBookAmmo":
                {
                    TryStartAddAmmoEffect(request, codeParams, Weapons.WEP_BOOK, AddBookAmmo, "added {0} to the Book supply.");
                    break;
                }

            default:
                Respond(request, EffectStatus.FailTemporary, StandardErrors.UnknownEffect, request);
                break;

                #endregion
        }
    }
}
#endregion
