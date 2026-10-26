using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace FlippingIsHardTrainer
{
    // Copied from FIH Ghost's NativeFileDialog.cs (https://github.com/d1ll3x3/FIH_Ghost),
    // itself the Map Editor's dialog extended with the Save dialog for exporting ghosts.
    //
    // Windows "Open" / "Save as" dialogs. IMGUI has no file picker under IL2CPP, so this
    // calls comdlg32 directly (no extra dependency). The dialog blocks the thread that shows
    // it, so it runs on its own STA thread and the game keeps rendering; the result is
    // handed to a callback on that thread, which must only marshal it to the main thread —
    // no Unity/IL2CPP calls from there.
    public static class NativeFileDialog
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OpenFileName
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public string lpstrFilter;
            public string lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public IntPtr lpstrFile;
            public int nMaxFile;
            public string lpstrFileTitle;
            public int nMaxFileTitle;
            public string lpstrInitialDir;
            public string lpstrTitle;
            public int Flags;
            public short nFileOffset;
            public short nFileExtension;
            public string lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public string lpTemplateName;
            public IntPtr pvReserved;
            public int dwReserved;
            public int FlagsEx;
        }

        [DllImport("comdlg32.dll", EntryPoint = "GetOpenFileNameW", CharSet = CharSet.Unicode)]
        private static extern bool GetOpenFileName(ref OpenFileName ofn);

        [DllImport("comdlg32.dll", EntryPoint = "GetSaveFileNameW", CharSet = CharSet.Unicode)]
        private static extern bool GetSaveFileName(ref OpenFileName ofn);

        [DllImport("comdlg32.dll")]
        private static extern int CommDlgExtendedError();

        private const int OFN_OVERWRITEPROMPT = 0x00000002;
        private const int OFN_NOCHANGEDIR = 0x00000008;   // keep the game's working directory
        private const int OFN_PATHMUSTEXIST = 0x00000800;
        private const int OFN_FILEMUSTEXIST = 0x00001000;
        private const int OFN_EXPLORER = 0x00080000;
        private const int MAX_PATH_CHARS = 4096;

        private const string GhostFilter = "FIH ghost (*.fihghost)\0*.fihghost\0\0";

        // onDone(path, error): path is null when cancelled or on failure; error is non-null
        // only on failure. Called on the dialog's thread.
        public static void OpenGhostFile(Action<string, string> onDone)
            => Show(false, "Import a ghost", null, onDone);

        public static void SaveGhostFile(string defaultName, Action<string, string> onDone)
            => Show(true, "Export the ghost", defaultName, onDone);

        private static void Show(bool save, string title, string defaultName, Action<string, string> onDone)
        {
            if (!OperatingSystem.IsWindows())
            {
                onDone(null, "the file dialog needs Windows");
                return;
            }
            var thread = new Thread(() =>
            {
                IntPtr buffer = IntPtr.Zero;
                try
                {
                    buffer = Marshal.AllocHGlobal(MAX_PATH_CHARS * 2);
                    Marshal.WriteInt16(buffer, 0);
                    if (!string.IsNullOrEmpty(defaultName))
                    {
                        // The buffer is also the dialog's initial file name.
                        var chars = (defaultName + "\0").ToCharArray();
                        Marshal.Copy(chars, 0, buffer, Math.Min(chars.Length, MAX_PATH_CHARS - 1));
                    }
                    var ofn = new OpenFileName
                    {
                        lStructSize = Marshal.SizeOf<OpenFileName>(),
                        // No owner window: owning the game's window from another thread would
                        // tie the dialog to the render thread's input queue and can stall it.
                        hwndOwner = IntPtr.Zero,
                        lpstrFilter = GhostFilter,
                        nFilterIndex = 1,
                        lpstrFile = buffer,
                        nMaxFile = MAX_PATH_CHARS,
                        lpstrTitle = title,
                        lpstrDefExt = "fihghost",
                        Flags = OFN_EXPLORER | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR
                                | (save ? OFN_OVERWRITEPROMPT : OFN_FILEMUSTEXIST),
                    };
                    if (save ? GetSaveFileName(ref ofn) : GetOpenFileName(ref ofn))
                    {
                        onDone(Marshal.PtrToStringUni(buffer), null);
                        return;
                    }
                    int code = CommDlgExtendedError();
                    onDone(null, code == 0 ? null : $"file dialog error 0x{code:X}");
                }
                catch (Exception ex)
                {
                    onDone(null, ex.Message);
                }
                finally
                {
                    if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
                }
            })
            {
                IsBackground = true,   // never keeps the game alive after it quits
                Name = "Trainer ghost file dialog",
            };
            thread.SetApartmentState(ApartmentState.STA);   // common dialogs require STA
            thread.Start();
        }
    }
}
