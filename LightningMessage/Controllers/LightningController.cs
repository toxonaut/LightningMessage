using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using LightningMessage.Models;
using Microsoft.AspNetCore.Mvc;
using System.Drawing;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.Drawing.Imaging;
using System.Text;
using System.Linq;

namespace LightningMessage.Controllers
{
    [Route("msg")]
    public class LightningController : Controller
    {
        private readonly IHostingEnvironment _hostingEnvironment;
        private AppDataProvider _myData;

        public LightningController(IApplicationLifetime appLifetime, IHostingEnvironment hostingEnvironment, AppDataProvider myAppDataProvider)
        {
            _hostingEnvironment = hostingEnvironment;
            _myData = myAppDataProvider;
        }

        [Route("")]
        [Route("index")]
        [Route("~/")]

        public IActionResult Index()
        {

            ViewData["Message"] = _myData.GetAppData.Message;
            string[] words = _myData.GetAppData.Message.Split(" ");
            int longestLength = words.OrderByDescending(s => s.Length).First().Length;
            if (longestLength > 16)
            {
                double newsize = 150d / longestLength;
                newsize = Math.Round(newsize, 2);
                ViewData["sizer"] = "font-size: " + newsize + "vw";
            }
            else
            {
                ViewData["sizer"] = "font-size: 10vw";
            }

            return View();
        }

        [Route("sendMessage/{*memo}")]
        public IActionResult SendMessage(string memo)
        {
            Lightning lnd = GetLightning();
            Google.Protobuf.ByteString RHash;
            string invoiceString;
            Bitmap qrCode;
            int satoshis = memo.Length;

            _myData.GetAppData.MessageTemp = memo;

            (invoiceString, qrCode, RHash) = lnd.GetInvoice((long)Convert.ToDouble(satoshis), memo);
            string filename = invoiceString.Substring(0, 20) + ".jpg";

            string outputFileName = Path.Combine(_myData.BillsPath, filename);
            HttpContext.Session.Set("QRFilename", Encoding.ASCII.GetBytes(outputFileName));

            using (MemoryStream memory = new MemoryStream())
            {
                using (FileStream fs = new FileStream(outputFileName, FileMode.Create, FileAccess.ReadWrite))
                {
                    qrCode.Save(memory, ImageFormat.Jpeg);
                    byte[] bytes = memory.ToArray();
                    fs.Write(bytes, 0, bytes.Length);
                }
            }

            byte[] hasharray = RHash.ToByteArray();
            HttpContext.Session.Set("Hash", hasharray);
            return new JsonResult(new { billFilename = filename, billText = invoiceString });
        }

        [Route("sendViewHistoryRequest")]
        public IActionResult ViewHistoryRequest()
        {
            Lightning lnd = GetLightning();
            Google.Protobuf.ByteString RHash;
            string invoiceString;
            Bitmap qrCode;
            int satoshis = 100;

            string memo = "View History";

            (invoiceString, qrCode, RHash) = lnd.GetInvoice((long)Convert.ToDouble(satoshis), memo);
            string filename = invoiceString.Substring(0, 20) + ".jpg";

            string outputFileName = Path.Combine(_myData.BillsPath, filename);

            HttpContext.Session.Set("QRFilenameHistory", Encoding.ASCII.GetBytes(outputFileName));

            using (MemoryStream memory = new MemoryStream())
            {
                using (FileStream fs = new FileStream(outputFileName, FileMode.Create, FileAccess.ReadWrite))
                {
                    qrCode.Save(memory, ImageFormat.Jpeg);
                    byte[] bytes = memory.ToArray();
                    fs.Write(bytes, 0, bytes.Length);
                }
            }

            byte[] hasharray = RHash.ToByteArray();
            HttpContext.Session.Set("HashHistory", hasharray);
            return new JsonResult(new { billFilename = filename, billText = invoiceString });
        }

        [Route("sendChannel/{*channelMemo}")]
        public IActionResult SendChannel(string channelMemo)
        {
            Lightning lnd = GetLightning();
            Google.Protobuf.ByteString RHash;
            string invoiceString;
            Bitmap qrCode;
            int satoshis = 25000;

            HttpContext.Session.Set("BuyChannel", Encoding.ASCII.GetBytes(channelMemo));

            (invoiceString, qrCode, RHash) = lnd.GetInvoice((long)Convert.ToDouble(satoshis), "Buy inbound channel to " + channelMemo);
            string filename = invoiceString.Substring(0, 20) + ".jpg";

            string outputFileName = Path.Combine(_myData.BillsPath, filename);
            HttpContext.Session.Set("QRFilenameChannel", Encoding.ASCII.GetBytes(outputFileName));

            using (MemoryStream memory = new MemoryStream())
            {
                using (FileStream fs = new FileStream(outputFileName, FileMode.Create, FileAccess.ReadWrite))
                {
                    qrCode.Save(memory, ImageFormat.Jpeg);
                    byte[] bytes = memory.ToArray();
                    fs.Write(bytes, 0, bytes.Length);
                }
            }

            byte[] hasharray = RHash.ToByteArray();
            HttpContext.Session.Set("HashChannel", hasharray);
            return new JsonResult(new { channelBillFilename = filename, channelBillText = invoiceString });
        }

        private Lightning GetLightning()
        {
            Lightning lnd = new Lightning(_myData.CertLocation, _myData.MacLocation, _myData.lndGRPC);
            return lnd;
        }

        [Route("checkpaid")]
        public async Task<IActionResult> CheckPay()
        {
            byte[] hasharray = HttpContext.Session.Get("Hash");
            Google.Protobuf.ByteString RHash = Google.Protobuf.ByteString.CopyFrom(hasharray);
            Lightning lnd = GetLightning();
            await lnd.ResponseStreaming(RHash);
            _myData.GetAppData.Message = _myData.GetAppData.MessageTemp;
            _myData.GetAppData.MessageStack.Enqueue(_myData.GetAppData.Message);
            _myData.GetAppData.MessageStack.Dequeue();
            _myData.Save();

            try
            {
                System.IO.File.Delete(Encoding.ASCII.GetString(HttpContext.Session.Get("QRFilename")));
            }
            catch
            {

            }
            return new JsonResult(new { paid = "paid.jpg", message = _myData.GetAppData.MessageTemp });
        }

        [Route("checkpaidHistory")]
        public async Task<IActionResult> CheckPaidHistory()
        {
            byte[] hasharray = HttpContext.Session.Get("HashHistory");
            Google.Protobuf.ByteString RHash = Google.Protobuf.ByteString.CopyFrom(hasharray);
            Lightning lnd = GetLightning();
            await lnd.ResponseStreaming(RHash);
            string returnMessage = "<ul>\n";
            for (int i = _myData.GetAppData.MessageStack.Count - 1; i >= 0; i--)
            {
                returnMessage += "<li>" + _myData.GetAppData.MessageStack.ToArray()[i] + "</li>\n";
            }
            returnMessage += "</ul>";

            _myData.SaveLog("View Messages");

            try
            {
                System.IO.File.Delete(Encoding.ASCII.GetString(HttpContext.Session.Get("QRFilenameHistory")));
            }
            catch
            {

            }
            return new JsonResult(returnMessage);
        }

        [Route("checkpaidChannel")]
        public async Task<IActionResult> CheckPaidChannel()
        {
            byte[] hasharray = HttpContext.Session.Get("HashChannel");
            Google.Protobuf.ByteString RHash = Google.Protobuf.ByteString.CopyFrom(hasharray);
            Lightning lnd = GetLightning();
            await lnd.ResponseStreaming(RHash);
            try
            {
                Email.WriteEmail("Me@lightningmessage.info", "martin.schaerer@gmail.com", "Open a channel", Encoding.ASCII.GetString(HttpContext.Session.Get("BuyChannel")));
                System.IO.File.Delete(Encoding.ASCII.GetString(HttpContext.Session.Get("QRFilenameChannel")));
            }
            catch
            {

            }
            return new JsonResult(new { paid = "paid.jpg", message = "Channel opening requested" });
        }

        [Route("checkmessageupdate/{message}")]
        public async Task<IActionResult> CheckMessageUpdate(string message)
        {
            await Task.Delay(5000);
            return new JsonResult(_myData.GetAppData.Message);
        }

        [Route("wait")]
        public async Task<IActionResult> Wait()
        {
            await Task.Delay(4000);
            return new JsonResult(null);
        }

        [Route("waitHistory")]
        public async Task<IActionResult> WaitHistory()
        {
            await Task.Delay(60000);
            return new JsonResult(null);
        }

    }
}
