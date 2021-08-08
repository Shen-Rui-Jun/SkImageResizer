using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;

namespace SkImageResizer
{
    public class SKImageProcess
    {
        /// <summary>
        /// 進行圖片的縮放作業
        /// </summary>
        /// <param name="sourcePath">圖片來源目錄路徑</param>
        /// <param name="destPath">產生圖片目的目錄路徑</param>
        /// <param name="scale">縮放比例</param>
        public void ResizeImages(string sourcePath, string destPath, double scale)
        {
            if (!Directory.Exists(destPath))
            {
                Directory.CreateDirectory(destPath);
            }

            var allFiles = FindImages(sourcePath);
            foreach (var filePath in allFiles)
            {
                var bitmap = SKBitmap.Decode(filePath);
                var imgPhoto = SKImage.FromBitmap(bitmap);
                var imgName = Path.GetFileNameWithoutExtension(filePath);

                var sourceWidth = imgPhoto.Width;
                var sourceHeight = imgPhoto.Height;

                var destinationWidth = (int)(sourceWidth * scale);
                var destinationHeight = (int)(sourceHeight * scale);

                using var scaledBitmap = bitmap.Resize(
                    new SKImageInfo(destinationWidth, destinationHeight),
                    SKFilterQuality.High);
                using var scaledImage = SKImage.FromBitmap(scaledBitmap);
                using var data = scaledImage.Encode(SKEncodedImageFormat.Jpeg, 100);
                using var s = File.OpenWrite(Path.Combine(destPath, imgName + ".jpg"));
                data.SaveTo(s);
            }
        }

        public async Task ResizeImagesAsync(string sourcePath, string destPath, double scale)
        {
            List<Task<AsyncData>> 前處理TaskList = new List<Task<AsyncData>>();
            List<Task<AsyncData>> 中處理TaskList = new List<Task<AsyncData>>();
            List<Task> 後處理TaskList = new List<Task>();

            List<AsyncData> 前處理結果List = new List<AsyncData>();
            List<AsyncData> 中處理結果List = new List<AsyncData>();

            if (!Directory.Exists(destPath))
            {
                Directory.CreateDirectory(destPath);
            }

            await Task.Yield();

            var allFiles = FindImages(sourcePath);

            foreach (var filePath in allFiles)
            {
                前處理TaskList.Add(Task.Run(() =>
                {
                    AsyncData tmpAsyncData = new AsyncData();
                    tmpAsyncData.Bitmap = SKBitmap.Decode(filePath);
                    tmpAsyncData.ImgPhoto = SKImage.FromBitmap(tmpAsyncData.Bitmap);
                    tmpAsyncData.ImgName = Path.GetFileNameWithoutExtension(filePath);

                    return tmpAsyncData;
                }));
            }

            前處理結果List = (await Task.WhenAll(前處理TaskList)).ToList();

            #region 效能 普遍在85.5下, 曾有一次提升至85.6%
            //foreach (var 前處理結果Data in 前處理結果List)
            //{
            //    後處理TaskList.Add(Task.Run(() =>
            //    {
            //        var sourceWidth = 前處理結果Data.ImgPhoto.Width;
            //        var sourceHeight = 前處理結果Data.ImgPhoto.Height;

            //        var destinationWidth = (int)(sourceWidth * scale);
            //        var destinationHeight = (int)(sourceHeight * scale);

            //        using var scaledBitmap = 前處理結果Data.Bitmap.Resize(
            //            new SKImageInfo(destinationWidth, destinationHeight),
            //            SKFilterQuality.High);
            //        using var scaledImage = SKImage.FromBitmap(scaledBitmap);
            //        using var data = scaledImage.Encode(SKEncodedImageFormat.Jpeg, 100);
            //        using var s = File.OpenWrite(Path.Combine(destPath, 前處理結果Data.ImgName + ".jpg"));
            //        data.SaveTo(s);
            //    }));
            //}
            //await Task.WhenAll(後處理TaskList);
            #endregion

            #region 效能 普遍在85.5%上, 曾有一次跌落至84%
            foreach (var 前處理結果Data in 前處理結果List)
            {
                中處理TaskList.Add(Task.Run(() =>
                {
                    AsyncData tmpAsyncData = new AsyncData();
                    var sourceWidth = 前處理結果Data.ImgPhoto.Width;
                    var sourceHeight = 前處理結果Data.ImgPhoto.Height;

                    var destinationWidth = (int)(sourceWidth * scale);
                    var destinationHeight = (int)(sourceHeight * scale);

                    using var scaledBitmap = 前處理結果Data.Bitmap.Resize(
                        new SKImageInfo(destinationWidth, destinationHeight),
                        SKFilterQuality.High);
                    using var scaledImage = SKImage.FromBitmap(scaledBitmap);
                    var data = scaledImage.Encode(SKEncodedImageFormat.Jpeg, 100);

                    tmpAsyncData.Data = data;
                    tmpAsyncData.ImgName = 前處理結果Data.ImgName;
                    return tmpAsyncData;
                }));
            }

            中處理結果List = (await Task.WhenAll(中處理TaskList)).ToList();

            foreach (var 中處理結果Data in 中處理結果List)
            {
                後處理TaskList.Add(Task.Run(() =>
                {
                    using var s = File.OpenWrite(Path.Combine(destPath, 中處理結果Data.ImgName + ".jpg"));
                    中處理結果Data.Data.SaveTo(s);
                }));
            }

            await Task.WhenAll(後處理TaskList);
            #endregion
        }

        /// <summary>
        /// 清空目的目錄下的所有檔案與目錄
        /// </summary>
        /// <param name="destPath">目錄路徑</param>
        public void Clean(string destPath)
        {
            if (!Directory.Exists(destPath))
            {
                Directory.CreateDirectory(destPath);
            }
            else
            {
                var allImageFiles = Directory.GetFiles(destPath, "*", SearchOption.AllDirectories);

                foreach (var item in allImageFiles)
                {
                    File.Delete(item);
                }
            }
        }

        /// <summary>
        /// 找出指定目錄下的圖片
        /// </summary>
        /// <param name="srcPath">圖片來源目錄路徑</param>
        /// <returns></returns>
        public List<string> FindImages(string srcPath)
        {
            List<string> files = new List<string>();
            files.AddRange(Directory.GetFiles(srcPath, "*.png", SearchOption.AllDirectories));
            files.AddRange(Directory.GetFiles(srcPath, "*.jpg", SearchOption.AllDirectories));
            files.AddRange(Directory.GetFiles(srcPath, "*.jpeg", SearchOption.AllDirectories));
            return files;
        }

        private class AsyncData
        {
            /// <summary>
            /// 存放 SkiaSharp Lib 的 Bitmap 型別的檔案
            /// </summary>
            public SKBitmap Bitmap { get; set; }

            /// <summary>
            /// 存放 SkiaSharp Lib 的圖像資料
            /// </summary>
            public SKImage ImgPhoto { get; set; }

            /// <summary>
            /// 圖檔名稱
            /// </summary>
            public string ImgName { get; set; }

            /// <summary>
            /// 存放 Data 的 Buffer
            /// </summary>
            public SKData Data { get; set; }
        }
    }
}