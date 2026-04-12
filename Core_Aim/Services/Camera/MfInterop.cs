using System;
using System.Runtime.InteropServices;

namespace Core_Aim.Services.Camera
{
    // ═══════════════════════════════════════════════════════════════════════
    // Media Foundation COM Interop — definições mínimas para IMFSourceReader
    // com captura nativa NV12/YUY2 em 4K@60fps com baixa latência.
    // ═══════════════════════════════════════════════════════════════════════

    internal static class MfInterop
    {
        // ── DLL imports ───────────────────────────────────────────────────
        [DllImport("mfplat.dll")]
        public static extern int MFStartup(uint version, uint dwFlags = 0);

        [DllImport("mfplat.dll")]
        public static extern int MFShutdown();

        [DllImport("mfplat.dll")]
        public static extern int MFCreateMediaType(out IMFMediaType ppMFType);

        [DllImport("mfplat.dll")]
        public static extern int MFCreateAttributes(out IMFAttributes ppMFAttributes, uint cInitialSize);

        [DllImport("mfreadwrite.dll")]
        public static extern int MFCreateSourceReaderFromMediaSource(
            IMFMediaSource pMediaSource,
            IMFAttributes? pAttributes,
            out IMFSourceReader ppSourceReader);

        [DllImport("mf.dll")]
        public static extern int MFEnumDeviceSources(
            IMFAttributes pAttributes,
            out IntPtr pppSourceActivate,  // IMFActivate**
            out uint pcSourceActivate);

        // ── Constants ─────────────────────────────────────────────────────
        public const uint MF_VERSION = 0x00020070; // MF 2.0
        public const uint MF_SOURCE_READER_FIRST_VIDEO_STREAM = 0xFFFFFFFC;
        public const int  MF_E_INVALIDREQUEST = unchecked((int)0xC00D36B2);
        public const int  MF_E_NO_MORE_TYPES  = unchecked((int)0xC00D36B9);

        // ── GUIDs ─────────────────────────────────────────────────────────

        // Attribute keys
        public static readonly Guid MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE
            = new("c60ac5fe-252a-478f-a0ef-bc8fa5f7cad3");
        public static readonly Guid MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID
            = new("8ac3587a-4ae7-42d8-99e0-0a6013eef90f");
        public static readonly Guid MF_DEVSOURCE_ATTRIBUTE_FRIENDLY_NAME
            = new("60d0e559-52f8-4fa2-bbce-acdb34a8ec01");
        public static readonly Guid MF_SOURCE_READER_ENABLE_ADVANCED_VIDEO_PROCESSING
            = new("f81da2c0-b537-4672-a8b2-a681b17307a3");
        public static readonly Guid MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS
            = new("a634a91c-822b-41b9-a494-4de4643612b0");
        public static readonly Guid MF_LOW_LATENCY
            = new("9c27891a-ed7a-40e1-88e8-b22727a024ee");
        public static readonly Guid MF_SOURCE_READER_DISCONNECT_MEDIASOURCE_ON_SHUTDOWN
            = new("56b67165-219e-456d-a22e-2d3004c7fe56");

        // Media type attributes
        public static readonly Guid MF_MT_MAJOR_TYPE
            = new("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
        public static readonly Guid MF_MT_SUBTYPE
            = new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
        public static readonly Guid MF_MT_FRAME_SIZE
            = new("1652c33d-d6b2-4012-b834-72030849a37d");
        public static readonly Guid MF_MT_FRAME_RATE
            = new("c459a2e8-3d2c-4e44-b132-fee5156c7bb0");
        public static readonly Guid MF_MT_DEFAULT_STRIDE
            = new("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");

        // Major types
        public static readonly Guid MFMediaType_Video
            = new("73646976-0000-0010-8000-00AA00389B71");

        // Subtypes (pixel formats)
        public static readonly Guid MFVideoFormat_NV12
            = new("3231564E-0000-0010-8000-00AA00389B71");
        public static readonly Guid MFVideoFormat_YUY2
            = new("32595559-0000-0010-8000-00AA00389B71");
        public static readonly Guid MFVideoFormat_RGB24
            = new("00000014-0000-0010-8000-00AA00389B71");
        public static readonly Guid MFVideoFormat_RGB32
            = new("00000016-0000-0010-8000-00AA00389B71");
        public static readonly Guid MFVideoFormat_MJPG
            = new("47504A4D-0000-0010-8000-00AA00389B71");
        public static readonly Guid MFVideoFormat_UYVY
            = new("59565955-0000-0010-8000-00AA00389B71");

        // ── Helper: Pack/Unpack 64-bit size/rate ──────────────────────────
        public static ulong Pack2x32(uint hi, uint lo) => ((ulong)hi << 32) | lo;
        public static (uint hi, uint lo) Unpack2x32(ulong val) => ((uint)(val >> 32), (uint)(val & 0xFFFFFFFF));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COM Interfaces
    // ═══════════════════════════════════════════════════════════════════════

    [ComImport, Guid("2CD2D921-C447-44A7-A13C-4ADABFC247E3"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMFAttributes
    {
        // IMFAttributes has many methods — we only need a few.
        // Slots 0-5: GetItem, GetItemType, CompareItem, Compare, GetUINT32, GetUINT64
        void GetItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, IntPtr pValue);
        void GetItemType([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out uint pType);
        void CompareItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, IntPtr Value, [MarshalAs(UnmanagedType.Bool)] out bool pbResult);
        void Compare(IMFAttributes pTheirs, uint MatchType, [MarshalAs(UnmanagedType.Bool)] out bool pbResult);
        void GetUINT32([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out uint punValue);
        void GetUINT64([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out ulong punValue);

        // Slot 6: GetDouble
        void GetDouble([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out double pfValue);

        // Slot 7: GetGUID
        void GetGUID([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out Guid pguidValue);

        // Slot 8: GetStringLength
        void GetStringLength([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out uint pcchLength);

        // Slot 9: GetString
        void GetString([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey,
                        [Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pwszValue,
                        uint cchBufSize, out uint pcchLength);

        // Slot 10: GetAllocatedString
        void GetAllocatedString([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey,
                                 [MarshalAs(UnmanagedType.LPWStr)] out string ppwszValue, out uint pcchLength);

        // Slot 11: GetBlobSize
        void GetBlobSize([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out uint pcbBlobSize);

        // Slot 12: GetBlob
        void GetBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey,
                      [Out, MarshalAs(UnmanagedType.LPArray)] byte[] pBuf, uint cbBufSize, out uint pcbBlobSize);

        // Slot 13: GetAllocatedBlob
        void GetAllocatedBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out IntPtr ppBuf, out uint pcbSize);

        // Slot 14: GetUnknown
        void GetUnknown([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey,
                         [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid,
                         [MarshalAs(UnmanagedType.IUnknown)] out object ppv);

        // Slot 15-16: SetItem, DeleteItem
        void SetItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, IntPtr Value);
        void DeleteItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey);

        // Slot 17: DeleteAllItems
        void DeleteAllItems();

        // Slot 18: SetUINT32
        void SetUINT32([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, uint unValue);

        // Slot 19: SetUINT64
        void SetUINT64([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, ulong unValue);

        // Slot 20: SetDouble
        void SetDouble([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, double fValue);

        // Slot 21: SetGUID
        void SetGUID([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, MarshalAs(UnmanagedType.LPStruct)] Guid guidValue);

        // Slot 22: SetString
        void SetString([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, MarshalAs(UnmanagedType.LPWStr)] string wszValue);

        // Slot 23: SetBlob
        void SetBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey,
                      [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] pBuf, uint cbBufSize);

        // Slot 24: SetUnknown
        void SetUnknown([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, MarshalAs(UnmanagedType.IUnknown)] object? pUnknown);

        // Slot 25-26: LockStore, UnlockStore
        void LockStore();
        void UnlockStore();

        // Slot 27: GetCount
        void GetCount(out uint pcItems);

        // Slot 28: GetItemByIndex
        void GetItemByIndex(uint unIndex, out Guid pguidKey, IntPtr pValue);

        // Slot 29: CopyAllItems
        void CopyAllItems(IMFAttributes pDest);
    }

    [ComImport, Guid("44AE0FA8-EA31-4109-8D2E-4CAE4997C555"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMFMediaType : IMFAttributes
    {
        // IMFAttributes vtable slots 0-29 are inherited

        // IMFMediaType slots
        new void GetItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, IntPtr pValue);
        new void GetItemType([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out uint pType);
        new void CompareItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, IntPtr Value, [MarshalAs(UnmanagedType.Bool)] out bool pbResult);
        new void Compare(IMFAttributes pTheirs, uint MatchType, [MarshalAs(UnmanagedType.Bool)] out bool pbResult);
        new void GetUINT32([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out uint punValue);
        new void GetUINT64([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out ulong punValue);
        new void GetDouble([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out double pfValue);
        new void GetGUID([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out Guid pguidValue);
        new void GetStringLength([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out uint pcchLength);
        new void GetString([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey,
                            [Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pwszValue,
                            uint cchBufSize, out uint pcchLength);
        new void GetAllocatedString([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey,
                                     [MarshalAs(UnmanagedType.LPWStr)] out string ppwszValue, out uint pcchLength);
        new void GetBlobSize([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out uint pcbBlobSize);
        new void GetBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey,
                          [Out, MarshalAs(UnmanagedType.LPArray)] byte[] pBuf, uint cbBufSize, out uint pcbBlobSize);
        new void GetAllocatedBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out IntPtr ppBuf, out uint pcbSize);
        new void GetUnknown([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey,
                             [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid,
                             [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
        new void SetItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, IntPtr Value);
        new void DeleteItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey);
        new void DeleteAllItems();
        new void SetUINT32([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, uint unValue);
        new void SetUINT64([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, ulong unValue);
        new void SetDouble([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, double fValue);
        new void SetGUID([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, MarshalAs(UnmanagedType.LPStruct)] Guid guidValue);
        new void SetString([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, MarshalAs(UnmanagedType.LPWStr)] string wszValue);
        new void SetBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey,
                          [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] pBuf, uint cbBufSize);
        new void SetUnknown([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, MarshalAs(UnmanagedType.IUnknown)] object? pUnknown);
        new void LockStore();
        new void UnlockStore();
        new void GetCount(out uint pcItems);
        new void GetItemByIndex(uint unIndex, out Guid pguidKey, IntPtr pValue);
        new void CopyAllItems(IMFAttributes pDest);

        // IMFMediaType-specific
        [PreserveSig] int GetMajorType(out Guid pguidMajorType);
        [PreserveSig] int IsCompressedFormat([MarshalAs(UnmanagedType.Bool)] out bool pfCompressed);
        [PreserveSig] int IsEqual(IMFMediaType pIMediaType, out uint pdwFlags);
        [PreserveSig] int GetRepresentation(Guid guidRepresentation, out IntPtr ppvRepresentation);
        [PreserveSig] int FreeRepresentation(Guid guidRepresentation, IntPtr pvRepresentation);
    }

    [ComImport, Guid("70AE66F2-C809-4E4F-8915-BDCB406B7993"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMFSourceReader
    {
        [PreserveSig]
        int GetStreamSelection(uint dwStreamIndex, [MarshalAs(UnmanagedType.Bool)] out bool pfSelected);

        [PreserveSig]
        int SetStreamSelection(uint dwStreamIndex, [MarshalAs(UnmanagedType.Bool)] bool fSelected);

        [PreserveSig]
        int GetNativeMediaType(uint dwStreamIndex, uint dwMediaTypeIndex, out IMFMediaType ppMediaType);

        [PreserveSig]
        int GetCurrentMediaType(uint dwStreamIndex, out IMFMediaType ppMediaType);

        [PreserveSig]
        int SetCurrentMediaType(uint dwStreamIndex, IntPtr pdwReserved, IMFMediaType pMediaType);

        [PreserveSig]
        int SetCurrentPosition([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidTimeFormat, IntPtr varPosition);

        [PreserveSig]
        int ReadSample(
            uint dwStreamIndex,
            uint dwControlFlags,
            out uint pdwActualStreamIndex,
            out uint pdwStreamFlags,
            out long pllTimestamp,
            out IMFSample? ppSample);

        [PreserveSig]
        int Flush(uint dwStreamIndex);
    }

    [ComImport, Guid("C40A00F2-B93A-4D80-AE8C-5A1A54B8B14B"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMFSample
    {
        // IMFAttributes slots (inherited) — 30 methods
        void GetItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, IntPtr pValue);
        void GetItemType([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out uint pType);
        void CompareItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, IntPtr Value, [MarshalAs(UnmanagedType.Bool)] out bool pbResult);
        void Compare(IMFAttributes pTheirs, uint MatchType, [MarshalAs(UnmanagedType.Bool)] out bool pbResult);
        void GetUINT32([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out uint punValue);
        void GetUINT64([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out ulong punValue);
        void GetDouble([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out double pfValue);
        void GetGUID([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out Guid pguidValue);
        void GetStringLength([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out uint pcchLength);
        void GetString([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey,
                        [Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pwszValue,
                        uint cchBufSize, out uint pcchLength);
        void GetAllocatedString([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey,
                                 [MarshalAs(UnmanagedType.LPWStr)] out string ppwszValue, out uint pcchLength);
        void GetBlobSize([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out uint pcbBlobSize);
        void GetBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey,
                      [Out, MarshalAs(UnmanagedType.LPArray)] byte[] pBuf, uint cbBufSize, out uint pcbBlobSize);
        void GetAllocatedBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out IntPtr ppBuf, out uint pcbSize);
        void GetUnknown([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey,
                         [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid,
                         [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
        void SetItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, IntPtr Value);
        void DeleteItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey);
        void DeleteAllItems();
        void SetUINT32([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, uint unValue);
        void SetUINT64([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, ulong unValue);
        void SetDouble([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, double fValue);
        void SetGUID([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, MarshalAs(UnmanagedType.LPStruct)] Guid guidValue);
        void SetString([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, MarshalAs(UnmanagedType.LPWStr)] string wszValue);
        void SetBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey,
                      [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] pBuf, uint cbBufSize);
        void SetUnknown([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, MarshalAs(UnmanagedType.IUnknown)] object? pUnknown);
        void LockStore();
        void UnlockStore();
        void GetCount(out uint pcItems);
        void GetItemByIndex(uint unIndex, out Guid pguidKey, IntPtr pValue);
        void CopyAllItems(IMFAttributes pDest);

        // IMFSample-specific
        [PreserveSig] int GetSampleFlags(out uint pdwSampleFlags);
        [PreserveSig] int SetSampleFlags(uint dwSampleFlags);
        [PreserveSig] int GetSampleTime(out long phnsSampleTime);
        [PreserveSig] int SetSampleTime(long hnsSampleTime);
        [PreserveSig] int GetSampleDuration(out long phnsSampleDuration);
        [PreserveSig] int SetSampleDuration(long hnsSampleDuration);
        [PreserveSig] int GetBufferCount(out uint pdwBufferCount);
        [PreserveSig] int GetBufferByIndex(uint dwIndex, out IMFMediaBuffer ppBuffer);
        [PreserveSig] int ConvertToContiguousBuffer(out IMFMediaBuffer ppBuffer);
        [PreserveSig] int AddBuffer(IMFMediaBuffer pBuffer);
        [PreserveSig] int RemoveBufferByIndex(uint dwIndex);
        [PreserveSig] int RemoveAllBuffers();
        [PreserveSig] int GetTotalLength(out uint pcbTotalLength);
        [PreserveSig] int CopyToBuffer(IMFMediaBuffer pBuffer);
    }

    [ComImport, Guid("045FA593-8799-42b8-BC8D-8968C6453507"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMFMediaBuffer
    {
        [PreserveSig] int Lock(out IntPtr ppbBuffer, out int pcbMaxLength, out int pcbCurrentLength);
        [PreserveSig] int Unlock();
        [PreserveSig] int GetCurrentLength(out int pcbCurrentLength);
        [PreserveSig] int SetCurrentLength(int cbCurrentLength);
        [PreserveSig] int GetMaxLength(out int pcbMaxLength);
    }

    [ComImport, Guid("7DC9D5F9-9ED9-44ec-9BBF-0600BB589FBB"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMF2DBuffer
    {
        [PreserveSig] int Lock2D(out IntPtr ppbScanline0, out int plPitch);
        [PreserveSig] int Unlock2D();
        [PreserveSig] int GetScanline0AndPitch(out IntPtr pbScanline0, out int plPitch);
        [PreserveSig] int IsContiguousFormat([MarshalAs(UnmanagedType.Bool)] out bool pfIsContiguous);
        [PreserveSig] int GetContiguousLength(out int pcbLength);
        [PreserveSig] int ContiguousCopyTo(IntPtr pbDestBuffer, int cbDestBuffer);
        [PreserveSig] int ContiguousCopyFrom(IntPtr pbSrcBuffer, int cbSrcBuffer);
    }

    [ComImport, Guid("7FEE9E9A-4A89-47A6-899C-B6A53A70FB67"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMFActivate : IMFAttributes
    {
        // IMFAttributes vtable — inherited (30 methods)
        new void GetItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, IntPtr pValue);
        new void GetItemType([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out uint pType);
        new void CompareItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, IntPtr Value, [MarshalAs(UnmanagedType.Bool)] out bool pbResult);
        new void Compare(IMFAttributes pTheirs, uint MatchType, [MarshalAs(UnmanagedType.Bool)] out bool pbResult);
        new void GetUINT32([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out uint punValue);
        new void GetUINT64([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out ulong punValue);
        new void GetDouble([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out double pfValue);
        new void GetGUID([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out Guid pguidValue);
        new void GetStringLength([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out uint pcchLength);
        new void GetString([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey,
                            [Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pwszValue,
                            uint cchBufSize, out uint pcchLength);
        new void GetAllocatedString([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey,
                                     [MarshalAs(UnmanagedType.LPWStr)] out string ppwszValue, out uint pcchLength);
        new void GetBlobSize([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out uint pcbBlobSize);
        new void GetBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey,
                          [Out, MarshalAs(UnmanagedType.LPArray)] byte[] pBuf, uint cbBufSize, out uint pcbBlobSize);
        new void GetAllocatedBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out IntPtr ppBuf, out uint pcbSize);
        new void GetUnknown([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey,
                             [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid,
                             [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
        new void SetItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, IntPtr Value);
        new void DeleteItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey);
        new void DeleteAllItems();
        new void SetUINT32([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, uint unValue);
        new void SetUINT64([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, ulong unValue);
        new void SetDouble([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, double fValue);
        new void SetGUID([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, MarshalAs(UnmanagedType.LPStruct)] Guid guidValue);
        new void SetString([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, MarshalAs(UnmanagedType.LPWStr)] string wszValue);
        new void SetBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey,
                          [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] pBuf, uint cbBufSize);
        new void SetUnknown([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, MarshalAs(UnmanagedType.IUnknown)] object? pUnknown);
        new void LockStore();
        new void UnlockStore();
        new void GetCount(out uint pcItems);
        new void GetItemByIndex(uint unIndex, out Guid pguidKey, IntPtr pValue);
        new void CopyAllItems(IMFAttributes pDest);

        // IMFActivate-specific
        [PreserveSig] int ActivateObject([In, MarshalAs(UnmanagedType.LPStruct)] Guid riid,
                                          [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
        [PreserveSig] int ShutdownObject();
        [PreserveSig] int DetachObject();
    }

    [ComImport, Guid("279A808D-AEC7-40C8-9C6B-A6B492C78A66"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMFMediaSource
    {
        // IMFMediaEventGenerator (4 methods)
        [PreserveSig] int _wireGetEvent(uint dwFlags, out IntPtr ppEvent);
        [PreserveSig] int _wireBeginGetEvent(IntPtr pCallback, [MarshalAs(UnmanagedType.IUnknown)] object? punkState);
        [PreserveSig] int EndGetEvent(IntPtr pResult, out IntPtr ppEvent);
        [PreserveSig] int QueueEvent(uint met, [In, MarshalAs(UnmanagedType.LPStruct)] Guid guidExtendedType, int hrStatus, IntPtr pvValue);

        // IMFMediaSource
        [PreserveSig] int GetCharacteristics(out uint pdwCharacteristics);
        [PreserveSig] int CreatePresentationDescriptor(out IntPtr ppPresentationDescriptor);
        [PreserveSig] int Start(IntPtr pPresentationDescriptor, IntPtr pguidTimeFormat, IntPtr pvarStartPosition);
        [PreserveSig] int Stop();
        [PreserveSig] int Pause();
        [PreserveSig] int Shutdown();
    }
}
