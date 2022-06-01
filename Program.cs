using Aliyun.OSS;
using Aliyun.OSS.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OssUpload
{
    internal class Config
    {
        public static Config GetConfig()
        {
            string url = Path.Combine(Environment.CurrentDirectory, "config.json");
            if (File.Exists(url))
            {
                return JsonSerializer.Deserialize<Config>(File.ReadAllText(url));
            }
            File.WriteAllText(url, "{\"Settings\": {\"AccessKeyId\": \"1\",\"AccessKeySecret\": \"2\",\"Endpoint\": \"http://oss-cn-shenzhen.aliyuncs.com\",\"BucketName\": \"test\",\"ConcurrentUploadCount\": 5  } }");
            throw new Exception("缺失配置文件！");
        }

        public void SetConfig()
        {
            try
            {
                string url = Path.Combine(Environment.CurrentDirectory, "config.json");
                File.WriteAllText(url, JsonSerializer.Serialize(this));
                Console.WriteLine("保存成功！");
            }
            catch (Exception)
            {
                Console.WriteLine("修改成功！");
            }
        }

        public class OssSettings
        {
            public string AccessKeyId { get; set; }

            public string AccessKeySecret { get; set; }

            public string Endpoint { get; set; }

            public string BucketName { get; set; }

            public int ConcurrentUploadCount { get; set; }
        }

        public OssSettings Settings { get; init; }


        [System.Text.Json.Serialization.JsonIgnore]
        public OssClient Client = null;

        public Task<PutObjectResult> TaskPutObject(string bucketName, string key, string fileToUpload) => Task.Factory.FromAsync(Client.BeginPutObject, Client.EndPutObject, bucketName, key, fileToUpload, null);

        public Task<ObjectListing> TaskListObjects(ListObjectsRequest listObjectsRequest) => Task.Factory.FromAsync(Client.BeginListObjects, Client.EndListObjects, listObjectsRequest, null);

        public Task<UploadPartCopyResult> TaskUploadPartCopy(UploadPartCopyRequest uploadPartCopyRequest) => Task.Factory.FromAsync(Client.BeginUploadPartCopy, Client.EndUploadPartCopy, uploadPartCopyRequest, null);

        public Task<CopyObjectResult> TaskCopyObject(CopyObjectRequest copyObjectResult) => Task.Factory.FromAsync(Client.BeginCopyObject, Client.EndCopyResult, copyObjectResult, null);
        
        public Task<OssObject> TaskGetObject(GetObjectRequest getObjectRequest) => Task.Factory.FromAsync(Client.BeginGetObject, Client.EndGetObject, getObjectRequest, null);

    }

    class Program
    {
        public static Config Config;

        public static Config.OssSettings Settings => Config.Settings;
        public static string File_Size(long size)
        {
            double num = (double)size;
            string result;
            if (size / 1024L > 0L)
            {
                if (size / 1024L / 1024L > 0L)
                {
                    if (size / 1024L / 1024L / 1024L > 0L)
                    {
                        result = (num / 1024.0 / 1024.0 / 1024.0).ToString("0.00") + "GB";
                    }
                    else
                    {
                        result = (num / 1024.0 / 1024.0).ToString("0.00") + "MB";
                    }
                }
                else
                {
                    result = (num / 1024.0).ToString("0.00") + "KB";
                }
            }
            else
            {
                result = size.ToString("0.00") + "B";
            }
            return result;
        }

        public static string GetUrl()
        {
            Uri uri = new(Settings.Endpoint);
            return string.Format("http://{0}.{1}/", Settings.BucketName, uri.Host);
        }

        public static async Task PutObject(string bucketName, string DirectoryInfos, string VirtualDirectory)
        {
            try
            {
                Console.WriteLine("已着手上传的相关操作：");
                DirectoryInfo directoryInfo = new(DirectoryInfos);
                string oldValue = directoryInfo.Parent.FullName + "\\";
                FileInfo[] files = directoryInfo.GetFiles("*", SearchOption.AllDirectories);
                Console.WriteLine("开始上传（共计{0}个文件需要上传）：", files.Length);
                int count = 0, ActivityCount = -1;
                long size = 0;

                List<Task> tasks = new(Settings.ConcurrentUploadCount);

                for (int i = 0; i < Settings.ConcurrentUploadCount; i++)
                {
                    tasks.Add(PutObj(files, oldValue));
                }

                await Task.WhenAll(tasks);

                //foreach (FileInfo fileInfo in files)
                //{
                //    string text = VirtualDirectory + fileInfo.FullName.Replace(oldValue, "").Replace("\\", "/");

                //    if (text.Length > 1 && (text.StartsWith("/") || text.StartsWith("\\"))) text = text[1..];

                //    tasks.Add(Config.TaskPutObject(bucketName, text, fileInfo.FullName));

                //    //Config.Client.PutObject(bucketName, text, fileInfo.FullName);
                //    num++;
                //    Console.WriteLine("已上传：({0})，\n大小：{1}，\n进度：{2}/{3}", new object[]
                //    {
                //        text,
                //        Program.File_Size(fileInfo.Length),
                //        num,
                //        files.Length
                //    });
                //}

                Console.WriteLine("累计上传文件大小：{0}", File_Size(size));
                Console.WriteLine("已批量上传完成！");

                async Task PutObj(FileInfo[] files, string oldValue)
                {
                    do
                    {
                        int index = Interlocked.Increment(ref ActivityCount);

                        if (index >= files.Length) return;

                        var fileInfo = files[index];

                        string text = VirtualDirectory + fileInfo.FullName.Replace(oldValue, "").Replace("\\", "/");

                        if (text.Length > 1 && (text.StartsWith("/") || text.StartsWith("\\"))) text = text[1..];

                        try
                        {
                            await Config.TaskPutObject(bucketName, text, fileInfo.FullName);
                        }
                        catch (OssException ex)
                        {
                            Console.WriteLine("失败，错误代码： {0}; 错误信息: {1}. \nRequestID:{2}\tHostID:{3}", new object[]
                            {
                                ex.ErrorCode,
                                ex.Message,
                                ex.RequestId,
                                ex.HostId
                            });
                        }
                        catch (Exception ex2)
                        {
                            Console.WriteLine("失败，错误信息： {0}", ex2.Message);
                        }
                        //Config.Client.PutObject(bucketName, text, fileInfo.FullName);
                        //Interlocked.Increment(ref num);
                        //num++;
                        Console.WriteLine("已上传：({0})，\n大小：{1}，\n进度：{2}/{3}", new object[]
                        {
                            text,
                            File_Size(fileInfo.Length),
                            Interlocked.Increment(ref count),
                            files.Length
                        });

                        Interlocked.Add(ref size, fileInfo.Length);
                    } while (true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("失败，错误信息： {0}", ex.Message);
            }
        }

        public static async Task CopyObject(string oldValue, string bucketName, string targetBucket, string targetObject, (string, long)[] objs)
        {
            try
            {
                Console.WriteLine("已着手拷贝的相关操作：");
                Console.WriteLine("开始拷贝（共计{0}个文件需要拷贝）：", objs.Length);
                int count = 0, ActivityCount = -1;
                long size = 0;

                List<Task> tasks = new(Settings.ConcurrentUploadCount);

                if (targetObject.EndsWith('/')) targetObject = targetObject[..(targetObject.Length - 1)];

                for (int i = 0; i < Settings.ConcurrentUploadCount; i++)
                {
                    tasks.Add(PutObj(oldValue, targetObject));
                }

                await Task.WhenAll(tasks);

                Console.WriteLine("累计拷贝文件大小：{0}", File_Size(size));
                Console.WriteLine("已批量拷贝完成！");

                async Task PutObj(string oldValue, string targetObject)
                {
                    do
                    {
                        int index = Interlocked.Increment(ref ActivityCount);

                        if (index >= objs.Length) return;

                        var sourceObject = objs[index];

                        string oldstr;
                        if (oldValue.EndsWith('/'))
                        {
                            oldstr = sourceObject.Item1[(oldValue.Length-1)..];
                        }
                        else if (string.IsNullOrWhiteSpace(oldValue))
                        {
                            oldstr = sourceObject.Item1;
                            if (!oldstr.StartsWith('/')) oldstr = string.Concat('/', oldstr);
                        }
                        else
                        {
                            oldstr = sourceObject.Item1[oldValue.Length..];
                            oldstr = oldstr[oldstr.IndexOf('/')..];
                        }

                        string targeturl = string.Concat(targetObject, oldstr);

                        try
                        {
                            var metadata = new ObjectMetadata();
                            metadata.AddHeader("mk1", "mv1");
                            metadata.AddHeader("mk2", "mv2");
                            var req = new CopyObjectRequest(Settings.BucketName, sourceObject.Item1, targetBucket, targeturl)
                            {
                                // 如果NewObjectMetadata为null，则为COPY模式（即拷贝源文件的元信息），非null则为REPLACE模式（即覆盖源文件的元信息）。
                                NewObjectMetadata = metadata,
                                // 指定Object的版本ID。
                                SourceVersionId = null
                            };
                            await Config.TaskCopyObject(req);
                        }
                        catch (OssException ex)
                        {
                            Console.WriteLine("失败，错误代码： {0}; 错误信息: {1}. \nRequestID:{2}\tHostID:{3}", new object[]
                            {
                                ex.ErrorCode,
                                ex.Message,
                                ex.RequestId,
                                ex.HostId
                            });
                        }
                        catch (Exception ex2)
                        {
                            Console.WriteLine("失败，错误信息： {0}", ex2.Message);
                        }
                        //Config.Client.PutObject(bucketName, text, fileInfo.FullName);
                        //Interlocked.Increment(ref num);
                        //num++;
                        Console.WriteLine("已拷贝：({0})，\n大小：{1}，\n进度：{2}/{3}", new object[]
                        {
                            targeturl,
                            File_Size(sourceObject.Item2),
                            Interlocked.Increment(ref count),
                            objs.Length
                        });

                        Interlocked.Add(ref size, sourceObject.Item2);
                    } while (true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("失败，错误信息： {0}", ex.Message);
            }
        }

        public static async Task GetObject(string oldValue, string bucketName, string DirectoryInfos, (string, long)[] objs)
        {
            try
            {
                Console.WriteLine("已着手下载的相关操作：");
                //DirectoryInfo directoryInfo = new(DirectoryInfos);
                //string oldValue = directoryInfo.Parent.FullName + "\\";
                //FileInfo[] files = directoryInfo.GetFiles("*", SearchOption.AllDirectories);
                Console.WriteLine("开始下载（共计{0}个文件需要下载）：", objs.Length);
                int count = 0, ActivityCount = -1;
                long size = 0;

                List<Task> tasks = new(Settings.ConcurrentUploadCount);

                for (int i = 0; i < Settings.ConcurrentUploadCount; i++)
                {
                    tasks.Add(GetObj(objs, oldValue));
                }

                await Task.WhenAll(tasks);

                Console.WriteLine("累计下载文件大小：{0}", File_Size(size));
                Console.WriteLine("已批量下载完成！");

                async Task GetObj((string, long)[] objs, string oldValue)
                {
                    do
                    {
                        int index = Interlocked.Increment(ref ActivityCount);

                        if (index >= objs.Length) return;

                        var sourceObject = objs[index];

                        string oldstr;
                        if (oldValue.EndsWith('/'))
                        {
                            oldstr = sourceObject.Item1[(oldValue.Length - 1)..];
                        }
                        else if (string.IsNullOrWhiteSpace(oldValue))
                        {
                            oldstr = sourceObject.Item1;
                            if (!oldstr.StartsWith('/')) oldstr = string.Concat('/', oldstr);
                        }
                        else
                        {
                            oldstr = sourceObject.Item1[oldValue.Length..];
                            oldstr = oldstr[oldstr.IndexOf('/')..];
                        }

                        string downloadFilename = string.Concat(DirectoryInfos, oldstr);

                        var file = objs[index];

                        string objectName = file.Item1;

                        try
                        {
                            // 下载文件到流。OssObject包含了文件的各种信息，如文件所在的存储空间、文件名、元信息以及一个输入流。
                            var request = new GetObjectRequest(bucketName, objectName)
                            {
                                // 指定Object的版本id。
                                VersionId = null
                            };
                            var ossObject = await Config.TaskGetObject(request);
                            using (ossObject)
                                if (ossObject.IsSetResponseStream() && ossObject.ContentLength > 0)
                                {
                                    FileInfo fileInfo = new(downloadFilename);
                                    if (!Directory.Exists(fileInfo.DirectoryName))
                                    {
                                        Directory.CreateDirectory(fileInfo.DirectoryName);
                                    }
                                    if (fileInfo.Exists) fileInfo.Delete();

                                    using var requestStream = ossObject.Content;
                                    int dsize = 1024 * 108;
                                    if (ossObject.ContentLength < dsize) dsize = (int)ossObject.ContentLength;
                                    Memory<byte> buf = new byte[dsize];
                                    var fs = fileInfo.Open(FileMode.OpenOrCreate);//File.Open(fileInfo.FullName, FileMode.OpenOrCreate);
                                    var len = 0;
                                    // 通过输入流将文件的内容读取到文件或者内存中。
                                    while ((len = await requestStream.ReadAsync(buf)) != 0)
                                    {
                                        await fs.WriteAsync(buf[0..len]);
                                    }
                                    fs.Close();
                                }
                        }
                        catch (OssException ex)
                        {
                            Console.WriteLine("失败，错误代码： {0}; 错误信息: {1}. \nRequestID:{2}\tHostID:{3}", new object[]
                            {
                                ex.ErrorCode,
                                ex.Message,
                                ex.RequestId,
                                ex.HostId
                            });
                        }
                        catch (Exception ex2)
                        {
                            Console.WriteLine("失败，错误信息： {0}", ex2.Message);
                        }
                        //Config.Client.PutObject(bucketName, text, fileInfo.FullName);
                        //Interlocked.Increment(ref num);
                        //num++;
                        Console.WriteLine("已下载：({0})，\n大小：{1}，\n进度：{2}/{3}", new object[]
                        {
                            objectName,
                            File_Size(file.Item2),
                            Interlocked.Increment(ref count),
                            objs.Length
                        });

                        Interlocked.Add(ref size, file.Item2);
                    } while (true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("失败，错误信息： {0}", ex.Message);
            }
        }
        public static async Task Main(string[] args)
        {
            Config = Config.GetConfig();

            try
            {
                Config.Client = new OssClient(Settings.Endpoint, Settings.AccessKeyId, Settings.AccessKeySecret);
            }
            catch (Exception ex)
            {
                Console.WriteLine("初始化异常，无法启动：错误内容：{0}", ex.Message);
                Console.ReadKey(true);
                return;
            }

            if (!Config.Client.DoesBucketExist(Settings.BucketName))
            {
                Console.WriteLine("您好：BucketName名，不存在！");
                return;
            }

            Console.WriteLine("欢迎使用批量上传工具：");
            string a = "0";
            do
            {
                try
                {
                    Console.WriteLine($"\n请选择是否上传：上传(1),查看(2),域名(3),删除(4),拷贝(5),下载(6),配置(7),取消(0)（当前选中库：{Settings.BucketName}）");
                    a = LimitKeyChar("1", "2", "3", "4", "5", "6", "7", "0");
                    if (a == "1")
                    {
                        Console.WriteLine("请输入批量上传的文件夹路径：（后回车/0取消）");
                        string directoryInfos = Console.ReadLine();
                        if (directoryInfos.Equals("0")) goto IL_336;
                        Console.WriteLine("请输上传OSS的虚拟路径：（后回车/0取消）");
                        string virtualDirectory = Console.ReadLine();
                        if (virtualDirectory.Equals("0")) goto IL_336;
                        await PutObject(Settings.BucketName, directoryInfos, virtualDirectory);
                    }
                    else if (a == "2")
                    {
                        Console.WriteLine("已着手获取当前云端目录信息：");
                        ListObjectsRequest listObjectsRequest = new(Settings.BucketName)
                        {
                            MaxKeys = new int?(1000),
                        };
                        Console.WriteLine("请输入查看路径（Max）：（后回车）");
                        string Prefix = Console.ReadLine();
                        if (!string.IsNullOrWhiteSpace(Prefix))
                        {
                            if (Prefix.Equals("/")) Prefix = "";
                            listObjectsRequest.Prefix = Prefix;
                        }
                        bool success = true;
                        int i = 0;

                        long size = 0;

                        while (success)
                        {
                            ObjectListing objectListing = await Config.TaskListObjects(listObjectsRequest);
                            Console.WriteLine("显示当前的OSS的文件目录：");
                            using IEnumerator<OssObjectSummary> enumerator = objectListing.ObjectSummaries.GetEnumerator();
                            while (enumerator.MoveNext())
                            {
                                OssObjectSummary ossObjectSummary = enumerator.Current;
                                Console.WriteLine("（{2}）文件路径：{0}，大小：{1}", ossObjectSummary.Key, File_Size(ossObjectSummary.Size), ++i);
                                size += ossObjectSummary.Size;
                            }
                            success = objectListing.IsTruncated;
                            listObjectsRequest.Marker = objectListing.NextMarker;
                        }

                        if (size > 0)
                        {
                            Console.WriteLine("累计检索文件大小：{0}", File_Size(size));
                        }
                        else
                        {
                            Console.WriteLine("未查找到任何文件！");
                        }
                    }
                    else if (a == "3")
                    {
                        Console.WriteLine("您的地址为：{0}", Program.GetUrl());
                    }
                    else if (a == "4")
                    {
                        List<string> list = new();
                        string a2 = "1";
                        do
                        {
                        IL_19B:
                            Console.WriteLine("删除指定文件Or文件夹（1/2）：");
                            string consoleKey = LimitKeyChar("1", "2");

                            if (consoleKey.Equals("1"))
                            {
                                Console.WriteLine("请输入文件地址：（后回车）");
                                string text = Console.ReadLine();
                                for (int i = 0; i < list.Count; i++)
                                {
                                    if (text == list[i])
                                    {
                                        Console.WriteLine("删除文件已存在！");
                                        goto IL_19B;
                                    }
                                }
                                Console.WriteLine("已着手验证文件是否存在：");
                                string text2 = text;
                                bool flag = Config.Client.DoesObjectExist(Settings.BucketName, text2);
                                if (flag)
                                {
                                    Console.WriteLine("验证结果：文件存在！");
                                    list.Add(text2);
                                    Console.WriteLine("是否继续添加删除文件？ 继续（1） ，结束（2），取消（0）");
                                    a2 = LimitKeyChar("1", "2", "0");
                                }
                                else
                                {
                                    Console.WriteLine("验证结果：文件不存在！");
                                    Console.WriteLine("是否重新添加删除文件？ 是（1），取消（0）");
                                    a2 = LimitKeyChar("1", "0");
                                }
                            }
                            else if (consoleKey.Equals("2"))
                            {
                                ListObjectsRequest listObjectsRequest = new(Settings.BucketName)
                                {
                                    MaxKeys = new int?(1000),
                                };
                                Console.WriteLine("请输入文件夹路径（未用‘/’结尾的采用模糊检索）：（后回车）");
                                string Prefix = Console.ReadLine();
                                if (!string.IsNullOrWhiteSpace(Prefix))
                                {
                                    if (Prefix.Equals("/")) Prefix = "";
                                    listObjectsRequest.Prefix = Prefix;
                                }
                                bool success = true;
                                int i = 0;
                                while (success)
                                {
                                    ObjectListing objectListing = await Config.TaskListObjects(listObjectsRequest);
                                    using IEnumerator<OssObjectSummary> enumerator = objectListing.ObjectSummaries.GetEnumerator();
                                    while (enumerator.MoveNext())
                                    {
                                        OssObjectSummary ossObjectSummary = enumerator.Current;
                                        if (!list.Contains(ossObjectSummary.Key))
                                        {
                                            list.Add(ossObjectSummary.Key);
                                            ++i;
                                        }
                                    }
                                    success = objectListing.IsTruncated;
                                    listObjectsRequest.Marker = objectListing.NextMarker;
                                    if (success) Console.Write("."); else Console.WriteLine();
                                }

                                Console.WriteLine("共计新增待删除：{0}条", i);
                                Console.WriteLine("是否继续添加删除文件？ 继续（1） ，结束（2），取消（0）");
                                a2 = LimitKeyChar("1", "2", "0");
                            }
                        }
                        while (a2 == "1");
                        if (a2 == "0")
                        {
                            Console.WriteLine("已取消删除操作！");
                        }
                        else
                        {
                            Console.WriteLine("开始着手删除已选中的文件！");
                            bool success = true;
                            int i = 0;
                            do
                            {
                                int index = i++ * 1000, rest = list.Count - index, count = rest > 1000 ? 1000 : rest;
                                if (rest > 0)
                                {
                                    var infos = list.GetRange(index, count);
                                    bool quiet = true;
                                    DeleteObjectsRequest deleteObjectsRequest = new(Settings.BucketName, infos, quiet);
                                    DeleteObjectsResult deleteObjectsResult = Config.Client.DeleteObjects(deleteObjectsRequest);
                                    if (deleteObjectsResult.Keys != null)
                                    {
                                        foreach (DeleteObjectsResult.DeletedObject deletedObject in deleteObjectsResult.Keys)
                                        {
                                            Console.WriteLine("未删除的文件：{0}，状态：{1}", deletedObject.Key, deleteObjectsResult.HttpStatusCode);
                                        }
                                    }

                                    Console.Write(".");
                                }
                                else
                                {
                                    success = false;
                                }
                            } while (success);
                            Console.WriteLine();
                            Console.WriteLine("已完成删除任务！");
                        }
                    }
                    else if (a == "5")
                    {
                        Console.WriteLine("拷贝指定文件Or文件夹（1/2）：");
                        string consoleKey = LimitKeyChar("1", "2");

                        if (consoleKey.Equals("1"))
                        {
                            Console.WriteLine("请输入要拷贝的文件路径：（后回车）");
                            string sourceObject = Console.ReadLine();
                            bool flag = Config.Client.DoesObjectExist(Settings.BucketName, sourceObject);
                            if (!flag)
                            {
                                Console.WriteLine("验证结果：文件不存在！");
                                goto IL_336;
                            }
                            Console.WriteLine("请输入拷贝到的文件路径：（后回车）");
                            string targetObject = Console.ReadLine();
                            Console.WriteLine("请输入拷贝到的库名空表示当前库：（后回车）");
                            string targetBucket = Console.ReadLine();
                            if (string.IsNullOrWhiteSpace(targetBucket)) targetBucket = Settings.BucketName;
                            var metadata = new ObjectMetadata();
                            metadata.AddHeader("mk1", "mv1");
                            metadata.AddHeader("mk2", "mv2");
                            var req = new CopyObjectRequest(Settings.BucketName, sourceObject, targetBucket, targetObject)
                            {
                                // 如果NewObjectMetadata为null，则为COPY模式（即拷贝源文件的元信息），非null则为REPLACE模式（即覆盖源文件的元信息）。
                                NewObjectMetadata = metadata,
                                // 指定Object的版本ID。
                                SourceVersionId = null
                            };
                            // 拷贝文件。
                            var result = await Config.TaskCopyObject(req);
                            Console.WriteLine("拷贝完成！");
                            //Console.WriteLine("Copy object succeeded, vesionid:{0}", result.VersionId);
                        }
                        else
                        {
                            List<(string, long)> list = new();

                            ListObjectsRequest listObjectsRequest = new(Settings.BucketName)
                            {
                                MaxKeys = new int?(1000),
                            };
                            Console.WriteLine("请输入文件夹路径（未用‘/’结尾的采用模糊检索）：（后回车）");
                            string Prefix = Console.ReadLine();
                            if (!string.IsNullOrWhiteSpace(Prefix))
                            {
                                if (Prefix.Equals("/")) Prefix = "";
                                listObjectsRequest.Prefix = Prefix;
                            }
                            bool success = true;
                            int i = 0;
                            while (success)
                            {
                                ObjectListing objectListing = await Config.TaskListObjects(listObjectsRequest);
                                using IEnumerator<OssObjectSummary> enumerator = objectListing.ObjectSummaries.GetEnumerator();
                                while (enumerator.MoveNext())
                                {
                                    OssObjectSummary ossObjectSummary = enumerator.Current;
                                    list.Add((ossObjectSummary.Key, ossObjectSummary.Size));
                                    ++i;
                                }
                                success = objectListing.IsTruncated;
                                listObjectsRequest.Marker = objectListing.NextMarker;
                                if (success) Console.Write("."); else Console.WriteLine();
                            }

                            Console.WriteLine("共计新增待拷贝：{0}条", i);
                            if (i > 0) {
                                Console.WriteLine("请输入拷贝到的路径：（后回车）");
                                string targetObject = Console.ReadLine();
                                Console.WriteLine("请输入拷贝到的库名空表示当前库：（后回车）");
                                string targetBucket = Console.ReadLine();
                                if (string.IsNullOrWhiteSpace(targetBucket)) targetBucket = Settings.BucketName;
                                await CopyObject(Prefix, Settings.BucketName, targetBucket, targetObject, list.ToArray());
                            }
                        }
                    }
                    else if (a == "6")
                    {
                        Console.WriteLine("下载指定文件Or文件夹（1/2）：");
                        string consoleKey = LimitKeyChar("1", "2");

                        if (consoleKey.Equals("1")) 
                        {
                            Console.WriteLine("请输入要下载的文件路径：（后回车）");
                            string sourceObject = Console.ReadLine();
                            bool flag = Config.Client.DoesObjectExist(Settings.BucketName, sourceObject);
                            if (!flag)
                            {
                                Console.WriteLine("验证结果：文件不存在！");
                                goto IL_336;
                            }
                            Console.WriteLine("请输入下载到磁盘的路径：（后回车）");
                            string targetObject = Console.ReadLine();
                            //await CopyObject(Prefix, Settings.BucketName, targetBucket, targetObject, list.ToArray());
                            await GetObject("", Settings.BucketName, targetObject, new (string, long)[] { (sourceObject,0) });
                        }
                        else
                        {
                            List<(string, long)> list = new();

                            ListObjectsRequest listObjectsRequest = new(Settings.BucketName)
                            {
                                MaxKeys = new int?(1000),
                            };
                            Console.WriteLine("请输入文件夹路径（未用‘/’结尾的采用模糊检索）：（后回车）");
                            string Prefix = Console.ReadLine();
                            if (!string.IsNullOrWhiteSpace(Prefix))
                            {
                                if (Prefix.Equals("/")) Prefix = "";
                                listObjectsRequest.Prefix = Prefix;
                            }
                            bool success = true;
                            int i = 0;
                            while (success)
                            {
                                ObjectListing objectListing = await Config.TaskListObjects(listObjectsRequest);
                                using IEnumerator<OssObjectSummary> enumerator = objectListing.ObjectSummaries.GetEnumerator();
                                while (enumerator.MoveNext())
                                {
                                    OssObjectSummary ossObjectSummary = enumerator.Current;
                                    list.Add((ossObjectSummary.Key, ossObjectSummary.Size));
                                    ++i;
                                }
                                success = objectListing.IsTruncated;
                                listObjectsRequest.Marker = objectListing.NextMarker;
                                if (success) Console.Write("."); else Console.WriteLine();
                            }

                            Console.WriteLine("共计待下载：{0}条", i);
                            if (i > 0)
                            {
                                Console.WriteLine("请输入要下载到的磁盘路径：（后回车）");
                                string targetObject = Console.ReadLine();
                                await GetObject(Prefix, Settings.BucketName, targetObject, list.ToArray());
                            }
                        }
                    }
                    else if (a == "7")
                    {
                        Console.WriteLine($"1.AccessKeyId：{Settings.AccessKeyId}\n2.AccessKeySecret：{Settings.AccessKeySecret}\n3.Endpoint：{Settings.Endpoint}\n4.BucketName：{Settings.BucketName}\n5.ConcurrentUploadCount：{Settings.ConcurrentUploadCount}");

                        Console.WriteLine($"请选择要修改的字段编号（0取消）");
                        string key = LimitKeyChar("1", "2", "3", "4", "5", "0");
                        if (!key.Equals("0"))
                        {
                            Console.WriteLine("请输入要修改成：（后回车/0取消）");
                            string val = Console.ReadLine();
                            if (val.Equals("0")) goto IL_336;
                            switch (key)
                            {
                                case "1":
                                    Settings.AccessKeyId = val;
                                    break;
                                case "2":
                                    Settings.AccessKeySecret = val;
                                    break;
                                case "3":
                                    Settings.Endpoint = val;
                                    break;
                                case "4":
                                    Settings.BucketName = val;
                                    break;
                                case "5":
                                    if (!int.TryParse(val, out int result))
                                    {
                                        Console.WriteLine("输入的值非有效正整数！");
                                        goto IL_336;
                                    }
                                    Settings.ConcurrentUploadCount = result;
                                    break;
                                default:
                                    goto IL_336;
                            }
                            Config.SetConfig();
                        }
                    }
                    else { break; }

                IL_336:;
                }
                catch (OssException ex2)
                {
                    Console.WriteLine("失败，错误代码： {0}; 错误信息: {1}. \nRequestID:{2}\tHostID:{3}", new object[]
                    {
                        ex2.ErrorCode,
                        ex2.Message,
                        ex2.RequestId,
                        ex2.HostId
                    });
                }
                catch (Exception ex3)
                {
                    if (ex3.Message.Equals("发生一个或多个错误。"))
                    {
                        if (ex3.InnerException.Message.Equals("无法发送具有此谓词类型的内容正文。"))
                        {
                            Console.WriteLine("失败，错误信息： {0}", "Endpoint：地址不正确！ OR 相关配置不正确！");
                        }
                        else
                        {
                            Console.WriteLine("失败，错误信息： {0}", ex3.InnerException.Message);
                        }
                    }
                    else
                    {
                        Console.WriteLine("失败，错误信息： {0}", ex3.Message);
                    }
                }
            }
            while (a != "0");
            Console.WriteLine("回车即结束！");
            Console.ReadKey(true);

            await Task.CompletedTask;
        }

        private static string LimitKeyChar(params string[] keys)
        {
            while (true)
            {
                string keychar = Console.ReadKey(true).KeyChar.ToString();
                foreach (var key in keys)
                {
                    if (key.Equals(keychar))
                    {
                        return keychar;
                    }
                }
            }
        }
    }
}
