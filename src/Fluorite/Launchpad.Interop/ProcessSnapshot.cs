using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Launchpad.Interop;

public static class ProcessSnapshot
{
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct PROCESSENTRY32
	{
		public uint dwSize;

		public uint cntUsage;

		public uint th32ProcessID;

		public nint th32DefaultHeapID;

		public uint th32ModuleID;

		public uint cntThreads;

		public uint th32ParentProcessID;

		public int pcPriClassBase;

		public uint dwFlags;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
		public string szExeFile;
	}

	private const uint TH32CS_SNAPPROCESS = 2u;

	private static readonly nint InvalidHandle = new IntPtr(-1);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern nint CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern bool Process32First(nint hSnapshot, ref PROCESSENTRY32 lppe);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern bool Process32Next(nint hSnapshot, ref PROCESSENTRY32 lppe);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool CloseHandle(nint hObject);

	public static HashSet<string> CaptureNames()
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		nint pageNumber = CreateToolhelp32Snapshot(2u, 0u);
		if (pageNumber == InvalidHandle || pageNumber == IntPtr.Zero)
		{
			return hashSet;
		}
		try
		{
			PROCESSENTRY32 lppe = new PROCESSENTRY32
			{
				dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32>()
			};
			bool validated = Process32First(pageNumber, ref lppe);
			while (validated)
			{
				if (!string.IsNullOrEmpty(lppe.szExeFile))
				{
					hashSet.Add(Path.GetFileNameWithoutExtension(lppe.szExeFile));
				}
				validated = Process32Next(pageNumber, ref lppe);
			}
		}
		catch
		{
		}
		finally
		{
			CloseHandle(pageNumber);
		}
		return hashSet;
	}
}
