using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Utilities.Utils;

public class CloudinaryUploader
{
    private string CLOUDINARY_URL;
    private Cloudinary cloudinary;

    public CloudinaryUploader(IConfiguration configuration)
    {
        CLOUDINARY_URL = "cloudinary://961444539646187:oLscUP38N7CtLwDwRrS8qrqAnyk@dyos46hhp";
        cloudinary = new Cloudinary(CLOUDINARY_URL);
        cloudinary.Api.Secure = true;
    }

    /**
     * Upload to the cloud and return an URL connect to it
     */
    public async Task<string?> UploadMediaAsync(IFormFile file)
    {
        var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg" };
        var videoExtensions = new[] { ".mp4", ".mov", ".avi", ".webm" };
        var extension = Path.GetExtension(file.FileName).ToLower();

        if (!imageExtensions.Contains(extension) && !videoExtensions.Contains(extension))
        {
            return "Unsupported file type";
        }

        var fileDesc = new FileDescription(file.FileName, file.OpenReadStream());

        if (imageExtensions.Contains(extension))
        {
            var imageParams = new ImageUploadParams
            {
                File = fileDesc,
                UseFilename = true,
                UniqueFilename = false,
                Overwrite = true
            };

            var uploadResult = await cloudinary.UploadAsync(imageParams);
            return uploadResult.Error == null ? uploadResult.Url?.AbsoluteUri : null;
        }
        else if (videoExtensions.Contains(extension))
        {
            var videoParams = new VideoUploadParams
            {
                File = fileDesc,
                UseFilename = true,
                UniqueFilename = false,
                Overwrite = true
            };

            var uploadResult = await cloudinary.UploadAsync(videoParams);
            return uploadResult.Error == null ? uploadResult.Url?.AbsoluteUri : null;
        }

        return null;
    }

    public async Task<string> UploadMultiMediaAsync(List<IFormFile> files, bool? checkValid = true)
    {
        if (checkValid == true && files.Count == 0) return "No file";

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg", ".mp4", ".mov", ".avi", ".webm" };
        List<string> result = new();

        foreach (var file in files)
        {
            var ext = Path.GetExtension(file.FileName).ToLower();

            if (checkValid == true && !allowedExtensions.Contains(ext))
            {
                return "Wrong extension: " + ext;
            }

            var url = await UploadMediaAsync(file);
            if (url != null)
            {
                result.Add(url);
            }
        }

        return string.Join(",", result);
    }
}