using Amazon.S3;
using Amazon.S3.Transfer;
using System.IO;
using System.Threading.Tasks;

namespace MovieStreamingApp.Services
{
    public class S3Service
    {
        private readonly string bucketName = "movie-streaming-app-bucket"; // Your S3 bucket name
        private readonly IAmazonS3 s3Client;

        public S3Service(IAmazonS3 s3Client)
        {
            this.s3Client = s3Client;
        }

        public async Task<string> UploadFileAsync(Stream fileStream, string fileName)
        {
            var uploadRequest = new TransferUtilityUploadRequest
            {
                InputStream = fileStream,
                Key = fileName,
                BucketName = bucketName,
                CannedACL = S3CannedACL.Private
            };

            var fileTransferUtility = new TransferUtility(s3Client);
            await fileTransferUtility.UploadAsync(uploadRequest);

            return $"https://{bucketName}.s3.amazonaws.com/{fileName}";
        }
    }
}
