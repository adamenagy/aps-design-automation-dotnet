using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Autodesk.Oss;
using Autodesk.Oss.Http;
using Autodesk.Oss.Model;

public partial class APS
{
    private async Task EnsureBucketExists(string bucketKey)
    {
        var auth = await GetInternalToken();
        var ossClient = new OssClient();
        try
        {
            await ossClient.GetBucketDetailsAsync(bucketKey, accessToken: auth.AccessToken);
        }
        catch (OssApiException ex)
        {
            if (ex.HttpResponseMessage.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                var payload = new CreateBucketsPayload
                {
                    BucketKey = bucketKey,
                    PolicyKey = PolicyKey.Transient
                };
                await ossClient.CreateBucketAsync(Region.US, payload, auth.AccessToken);
            }
            else
            {
                throw;
            }
        }
    }

    public async Task<ObjectDetails> UploadModel(string objectName, Stream stream)
    {
        await EnsureBucketExists(_bucket);
        var auth = await GetInternalToken();
        var ossClient = new OssClient();
        var objectDetails = await ossClient.UploadObjectAsync(_bucket, objectName, stream, accessToken: auth.AccessToken);
        return objectDetails;
    }

    public async Task<ObjectDetails> UploadModel(string objectName, string filePath)
    {
        using (var fileStream = File.OpenRead(filePath))
        {
            return await UploadModel(objectName, fileStream);
        }
    }

    public async Task<string> GetDownloadUrl(string fileName)
    {
        var auth = await GetInternalToken();
        var ossClient = new OssClient();
        var resource = await ossClient.CreateSignedResourceAsync(_bucket, fileName, null, Access.ReadWrite, true, auth.AccessToken);
        return resource.SignedUrl;
    }
}