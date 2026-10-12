using System;
using System.Runtime.InteropServices;
using System.Text;

namespace EstudiApp.Pdf
{
    /// <summary>Página renderizada en memoria: BGRA, filas de abajo hacia arriba (como espera Texture2D).</summary>
    public sealed class PdfBitmap
    {
        public int Width;
        public int Height;
        public byte[] Bgra;
    }

    /// <summary>
    /// Documento PDF abierto con PDFium. PDFium no admite llamadas simultáneas, así que todo pasa por un único
    /// candado: se puede renderizar o extraer texto desde un hilo de fondo sin bloquear la interfaz.
    /// </summary>
    public sealed class PdfDocument : IDisposable
    {
        private static readonly object Gate = new object();
        private static bool _initialized;

        private IntPtr _handle;

        public int PageCount { get; }
        public string Path { get; }

        private PdfDocument(IntPtr handle, string path)
        {
            _handle = handle;
            Path = path;
            PageCount = PdfiumNative.FPDF_GetPageCount(handle);
        }

        /// <summary>Indica si la biblioteca nativa está disponible en esta plataforma.</summary>
        public static bool Available
        {
            get
            {
                try
                {
                    EnsureInit();
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        private static void EnsureInit()
        {
            lock (Gate)
            {
                if (_initialized)
                    return;
                PdfiumNative.FPDF_InitLibrary();
                _initialized = true;
            }
        }

        public static PdfDocument Open(string path, string password = null)
        {
            EnsureInit();
            lock (Gate)
            {
                var handle = PdfiumNative.FPDF_LoadDocument(Utf8(path), password == null ? null : Utf8(password));
                if (handle == IntPtr.Zero)
                    throw new PdfException(ErrorMessage(PdfiumNative.FPDF_GetLastError()));
                return new PdfDocument(handle, path);
            }
        }

        private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value + "\0");

        private static string ErrorMessage(uint code)
        {
            switch (code)
            {
                case 2: return "No se encontró el archivo o no se pudo abrir.";
                case 3: return "El archivo no es un PDF válido o está dañado.";
                case 4: return "El PDF está protegido con contraseña.";
                case 5: return "El PDF tiene una protección no compatible.";
                default: return "No se pudo abrir el PDF.";
            }
        }

        public string Title
        {
            get
            {
                lock (Gate)
                {
                    if (_handle == IntPtr.Zero)
                        return null;
                    var tag = Encoding.ASCII.GetBytes("Title\0");
                    var length = PdfiumNative.FPDF_GetMetaText(_handle, tag, null, 0);
                    if (length <= 2)
                        return null;
                    var buffer = new byte[length];
                    PdfiumNative.FPDF_GetMetaText(_handle, tag, buffer, length);
                    var title = Encoding.Unicode.GetString(buffer, 0, (int)length - 2).Trim();
                    return title.Length == 0 ? null : title;
                }
            }
        }

        /// <summary>Tamaño de la página en puntos (1/72 de pulgada).</summary>
        public (float width, float height) PageSize(int index)
        {
            lock (Gate)
            {
                if (_handle == IntPtr.Zero || PdfiumNative.FPDF_GetPageSizeByIndex(_handle, index, out var w, out var h) == 0)
                    return (595f, 842f);
                return ((float)w, (float)h);
            }
        }

        /// <summary>Renderiza la página <paramref name="index"/> (desde 0) con el ancho en píxeles indicado.</summary>
        public PdfBitmap Render(int index, int width)
        {
            lock (Gate)
            {
                if (_handle == IntPtr.Zero)
                    throw new ObjectDisposedException(nameof(PdfDocument));
                var page = PdfiumNative.FPDF_LoadPage(_handle, index);
                if (page == IntPtr.Zero)
                    throw new PdfException("No se pudo cargar la página " + (index + 1) + ".");
                try
                {
                    var pageWidth = PdfiumNative.FPDF_GetPageWidth(page);
                    var pageHeight = PdfiumNative.FPDF_GetPageHeight(page);
                    width = Math.Max(16, width);
                    var height = Math.Max(16, (int)Math.Round(width * pageHeight / Math.Max(1.0, pageWidth)));
                    var bitmap = PdfiumNative.FPDFBitmap_Create(width, height, 0);
                    if (bitmap == IntPtr.Zero)
                        throw new PdfException("Memoria insuficiente para mostrar la página.");
                    try
                    {
                        PdfiumNative.FPDFBitmap_FillRect(bitmap, 0, 0, width, height, 0xFFFFFFFF);
                        PdfiumNative.FPDF_RenderPageBitmap(bitmap, page, 0, 0, width, height, 0, PdfiumNative.RenderAnnotations);
                        var stride = PdfiumNative.FPDFBitmap_GetStride(bitmap);
                        var source = PdfiumNative.FPDFBitmap_GetBuffer(bitmap);
                        var pixels = new byte[width * height * 4];
                        for (var y = 0; y < height; y++)
                            Marshal.Copy(source + y * stride, pixels, (height - 1 - y) * width * 4, width * 4);
                        return new PdfBitmap { Width = width, Height = height, Bgra = pixels };
                    }
                    finally
                    {
                        PdfiumNative.FPDFBitmap_Destroy(bitmap);
                    }
                }
                finally
                {
                    PdfiumNative.FPDF_ClosePage(page);
                }
            }
        }

        /// <summary>Texto de la página (vacío si es un escaneo sin capa de texto).</summary>
        public string PageText(int index)
        {
            lock (Gate)
            {
                if (_handle == IntPtr.Zero)
                    return string.Empty;
                var page = PdfiumNative.FPDF_LoadPage(_handle, index);
                if (page == IntPtr.Zero)
                    return string.Empty;
                var text = PdfiumNative.FPDFText_LoadPage(page);
                try
                {
                    if (text == IntPtr.Zero)
                        return string.Empty;
                    var count = PdfiumNative.FPDFText_CountChars(text);
                    if (count <= 0)
                        return string.Empty;
                    var buffer = new ushort[count + 1];
                    var written = PdfiumNative.FPDFText_GetText(text, 0, count, buffer);
                    var chars = new char[Math.Max(0, Math.Min(count, written - 1))];
                    for (var i = 0; i < chars.Length; i++)
                        chars[i] = (char)buffer[i];
                    return new string(chars);
                }
                finally
                {
                    if (text != IntPtr.Zero)
                        PdfiumNative.FPDFText_ClosePage(text);
                    PdfiumNative.FPDF_ClosePage(page);
                }
            }
        }

        public void Dispose()
        {
            lock (Gate)
            {
                if (_handle == IntPtr.Zero)
                    return;
                PdfiumNative.FPDF_CloseDocument(_handle);
                _handle = IntPtr.Zero;
            }
        }
    }

    public class PdfException : Exception
    {
        public PdfException(string message) : base(message)
        {
        }
    }
}
