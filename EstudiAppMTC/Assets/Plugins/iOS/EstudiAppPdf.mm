// Selector de documentos (Archivos, iCloud Drive) para importar un PDF. Unity llama a EstudiAppPdf_Start y consulta
// EstudiAppPdf_TakeResult hasta que deja de devolver "": "ok:ruta|nombre", "cancel" o "error:mensaje".
#import <UIKit/UIKit.h>
#import <UniformTypeIdentifiers/UniformTypeIdentifiers.h>

extern UIViewController *UnityGetGLViewController();

static NSString *gPdfResult = nil;

static void EstudiAppPdf_Finish(NSString *value)
{
    @synchronized ([UIDocumentPickerViewController class]) { gPdfResult = value; }
}

@interface EstudiAppPdfDelegate : NSObject <UIDocumentPickerDelegate>
@end

static EstudiAppPdfDelegate *gPdfDelegate = nil;

@implementation EstudiAppPdfDelegate

- (void)documentPicker:(UIDocumentPickerViewController *)controller didPickDocumentsAtURLs:(NSArray<NSURL *> *)urls
{
    NSURL *url = urls.firstObject;
    if (url == nil) { EstudiAppPdf_Finish(@"cancel"); return; }
    dispatch_async(dispatch_get_global_queue(QOS_CLASS_USER_INITIATED, 0), ^{
        BOOL scoped = [url startAccessingSecurityScopedResource];
        NSString *name = url.lastPathComponent ?: @"libro.pdf";
        NSString *file = [NSString stringWithFormat:@"import_%lld.pdf", (long long)([[NSDate date] timeIntervalSince1970] * 1000)];
        NSURL *target = [NSURL fileURLWithPath:[NSTemporaryDirectory() stringByAppendingPathComponent:file]];
        NSError *error = nil;
        BOOL ok = [[NSFileManager defaultManager] copyItemAtURL:url toURL:target error:&error];
        if (scoped)
            [url stopAccessingSecurityScopedResource];
        if (ok)
            EstudiAppPdf_Finish([NSString stringWithFormat:@"ok:%@|%@", target.path, [name stringByReplacingOccurrencesOfString:@"|" withString:@" "]]);
        else
            EstudiAppPdf_Finish([@"error:" stringByAppendingString:error.localizedDescription ?: @"No se pudo copiar el PDF."]);
    });
}

- (void)documentPickerWasCancelled:(UIDocumentPickerViewController *)controller
{
    EstudiAppPdf_Finish(@"cancel");
}

@end

extern "C" void EstudiAppPdf_Start()
{
    EstudiAppPdf_Finish(@"");
    dispatch_async(dispatch_get_main_queue(), ^{
        if (gPdfDelegate == nil)
            gPdfDelegate = [[EstudiAppPdfDelegate alloc] init];
        UIDocumentPickerViewController *picker = [[UIDocumentPickerViewController alloc] initForOpeningContentTypes:@[UTTypePDF] asCopy:YES];
        picker.delegate = gPdfDelegate;
        picker.allowsMultipleSelection = NO;
        picker.modalPresentationStyle = UIModalPresentationFormSheet;
        [UnityGetGLViewController() presentViewController:picker animated:YES completion:nil];
    });
}

extern "C" char *EstudiAppPdf_TakeResult()
{
    NSString *value;
    @synchronized ([UIDocumentPickerViewController class])
    {
        value = gPdfResult == nil ? @"none" : gPdfResult;
        if (gPdfResult != nil && gPdfResult.length > 0)
            gPdfResult = nil;
    }
    return strdup(value.UTF8String);
}
