using System;
using System.Runtime.InteropServices;

namespace EstudiApp.Pdf
{
    /// <summary>Funciones de PDFium (bblanchon/pdfium-binaries) usadas por el lector.</summary>
    internal static class PdfiumNative
    {
#if UNITY_IOS && !UNITY_EDITOR
        private const string Lib = "__Internal";
#else
        private const string Lib = "pdfium";
#endif

        public const int RenderAnnotations = 0x01;
        public const int RenderNoSmoothText = 0x1000;

        [DllImport(Lib)] public static extern void FPDF_InitLibrary();
        [DllImport(Lib)] public static extern uint FPDF_GetLastError();

        /// <param name="path">Ruta en UTF-8 terminada en 0.</param>
        [DllImport(Lib)] public static extern IntPtr FPDF_LoadDocument(byte[] path, byte[] password);
        [DllImport(Lib)] public static extern void FPDF_CloseDocument(IntPtr document);
        [DllImport(Lib)] public static extern int FPDF_GetPageCount(IntPtr document);
        [DllImport(Lib)] public static extern int FPDF_GetPageSizeByIndex(IntPtr document, int index, out double width, out double height);
        [DllImport(Lib)] public static extern uint FPDF_GetMetaText(IntPtr document, byte[] tag, byte[] buffer, uint length);

        [DllImport(Lib)] public static extern IntPtr FPDF_LoadPage(IntPtr document, int index);
        [DllImport(Lib)] public static extern void FPDF_ClosePage(IntPtr page);
        [DllImport(Lib)] public static extern double FPDF_GetPageWidth(IntPtr page);
        [DllImport(Lib)] public static extern double FPDF_GetPageHeight(IntPtr page);
        [DllImport(Lib)] public static extern void FPDF_RenderPageBitmap(IntPtr bitmap, IntPtr page, int startX, int startY, int sizeX, int sizeY, int rotate, int flags);

        [DllImport(Lib)] public static extern IntPtr FPDFBitmap_Create(int width, int height, int alpha);
        [DllImport(Lib)] public static extern int FPDFBitmap_FillRect(IntPtr bitmap, int left, int top, int width, int height, uint color);
        [DllImport(Lib)] public static extern IntPtr FPDFBitmap_GetBuffer(IntPtr bitmap);
        [DllImport(Lib)] public static extern int FPDFBitmap_GetStride(IntPtr bitmap);
        [DllImport(Lib)] public static extern void FPDFBitmap_Destroy(IntPtr bitmap);

        [DllImport(Lib)] public static extern IntPtr FPDFText_LoadPage(IntPtr page);
        [DllImport(Lib)] public static extern void FPDFText_ClosePage(IntPtr textPage);
        [DllImport(Lib)] public static extern int FPDFText_CountChars(IntPtr textPage);
        [DllImport(Lib)] public static extern int FPDFText_GetText(IntPtr textPage, int start, int count, [Out] ushort[] result);
    }
}
