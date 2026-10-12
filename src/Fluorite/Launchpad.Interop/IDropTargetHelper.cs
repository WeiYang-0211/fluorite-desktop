using System.Runtime.InteropServices;

namespace Launchpad.Interop;

[ComImport]
[Guid("4657278B-411B-11D2-839A-00C04FD918D0")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDropTargetHelper
{
	void DragEnter(nint hwndTarget, [MarshalAs(UnmanagedType.Interface)] object pDataObj, ref NativeMethods.POINT ppt, int dwEffect);

	void DragLeave();

	void DragOver(ref NativeMethods.POINT ppt, int dwEffect);

	void Drop([MarshalAs(UnmanagedType.Interface)] object pDataObj, ref NativeMethods.POINT ppt, int dwEffect);

	void Show(bool fShow);
}
