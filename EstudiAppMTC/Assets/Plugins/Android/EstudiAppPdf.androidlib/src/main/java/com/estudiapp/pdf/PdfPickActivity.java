package com.estudiapp.pdf;

import android.app.Activity;
import android.content.ActivityNotFoundException;
import android.content.Intent;
import android.database.Cursor;
import android.net.Uri;
import android.os.Bundle;
import android.provider.OpenableColumns;

import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.io.OutputStream;

/**
 * Abre el selector de documentos del sistema para elegir un PDF y lo copia a la caché de la app.
 * Unity llama a start() y consulta takeResult() hasta que deja de ser "": "ok:ruta|nombre", "cancel" o "error:mensaje".
 */
public class PdfPickActivity extends Activity {
    private static final int REQUEST = 4311;

    private static final Object LOCK = new Object();
    /** null: no hay pedido; "": en curso; si no, el resultado. */
    private static String result;

    public static void start(final Activity activity) {
        synchronized (LOCK) {
            result = "";
        }
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                try {
                    activity.startActivity(new Intent(activity, PdfPickActivity.class));
                } catch (Exception e) {
                    finishWith("error:" + e.getMessage());
                }
            }
        });
    }

    public static String takeResult() {
        synchronized (LOCK) {
            if (result == null)
                return "none";
            String value = result;
            if (!value.isEmpty())
                result = null;
            return value;
        }
    }

    private static void finishWith(String value) {
        synchronized (LOCK) {
            result = value;
        }
    }

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (savedInstanceState != null)
            return;
        try {
            Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT);
            intent.addCategory(Intent.CATEGORY_OPENABLE);
            intent.setType("application/pdf");
            startActivityForResult(intent, REQUEST);
        } catch (ActivityNotFoundException e) {
            finishWith("error:No hay un selector de documentos en este dispositivo.");
            finish();
        } catch (Exception e) {
            finishWith("error:" + e.getMessage());
            finish();
        }
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, final Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode != REQUEST)
            return;
        final Uri uri = data != null ? data.getData() : null;
        if (resultCode != RESULT_OK || uri == null) {
            finishWith("cancel");
            finish();
            return;
        }
        new Thread(new Runnable() {
            @Override
            public void run() {
                String value;
                try {
                    value = "ok:" + copy(uri);
                } catch (Throwable e) {
                    value = "error:" + e.getMessage();
                }
                finishWith(value);
                runOnUiThread(new Runnable() {
                    @Override
                    public void run() {
                        finish();
                    }
                });
            }
        }, "EstudiAppPdf").start();
    }

    private String copy(Uri uri) throws Exception {
        String name = "libro.pdf";
        try (Cursor cursor = getContentResolver().query(uri, new String[] { OpenableColumns.DISPLAY_NAME }, null, null, null)) {
            if (cursor != null && cursor.moveToFirst() && !cursor.isNull(0))
                name = cursor.getString(0);
        } catch (Exception ignored) {
            // Sin nombre: se usa el genérico.
        }
        File dir = new File(getCacheDir(), "estudiapp_import");
        if (!dir.exists())
            dir.mkdirs();
        File target = new File(dir, "import_" + System.currentTimeMillis() + ".pdf");
        try (InputStream in = getContentResolver().openInputStream(uri); OutputStream out = new FileOutputStream(target)) {
            if (in == null)
                throw new Exception("No se pudo leer el archivo.");
            byte[] buffer = new byte[1 << 16];
            int read;
            while ((read = in.read(buffer)) > 0)
                out.write(buffer, 0, read);
        }
        return target.getAbsolutePath() + "|" + name.replace('|', ' ');
    }
}
