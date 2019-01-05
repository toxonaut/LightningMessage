using Grpc.Core;
using Lnrpc;
using QRCoder;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;

namespace LightningMessage
{
    public class Lightning
    {
        private string tls { set; get; }
        private string macaroon { set; get; }
        private SslCredentials sslCreds { set; get; }
        private Lnrpc.Lightning.LightningClient client { get; set; }
        public Lightning(string tlsLocation, string macaroonLocation, string ip)
        {
            tls = File.ReadAllText(tlsLocation);
            sslCreds = new SslCredentials(tls);
            byte[] macaroonBytes = File.ReadAllBytes(macaroonLocation);
            macaroon = BitConverter.ToString(macaroonBytes).Replace("-", ""); // hex format stripped of "-" chars

            Task AddMacaroon(AuthInterceptorContext context, Metadata metadata)
            {
                metadata.Add(new Metadata.Entry("macaroon", macaroon));
                return Task.CompletedTask;
            }
            var macaroonInterceptor = new AsyncAuthInterceptor(AddMacaroon);
            var combinedCreds = ChannelCredentials.Create(sslCreds, CallCredentials.FromInterceptor(macaroonInterceptor));

            var channel = new Grpc.Core.Channel(ip, combinedCreds);
            client = new Lnrpc.Lightning.LightningClient(channel);
        }
        public (string, Bitmap, Google.Protobuf.ByteString) GetInvoice(long value, string memo)
        {
            var response = client.GetInfo(new GetInfoRequest());
            Console.WriteLine(response);
            var bal = client.WalletBalance(new WalletBalanceRequest());
            Console.WriteLine(bal);
            Invoice invoice = new Invoice();
            invoice.Value = value;
            invoice.Memo = memo;

            var opt = new CallOptions();
            var res = client.AddInvoice(invoice, opt);

            QRCodeGenerator qrGenerator = new QRCodeGenerator();
            QRCodeData qrCodeData = qrGenerator.CreateQrCode(res.PaymentRequest, QRCodeGenerator.ECCLevel.Q);
            QRCode qrCode = new QRCode(qrCodeData);
            Bitmap qrCodeImage = qrCode.GetGraphic(20);
            return (res.PaymentRequest, qrCodeImage, res.RHash);
        }
        public async Task ResponseStreaming(Google.Protobuf.ByteString RHash)
        {
            var request = new InvoiceSubscription();
            PaymentHash paymentHash = new PaymentHash();
            paymentHash.RHash = RHash;

            using (var call = client.SubscribeInvoices(request))
            {
                while (await call.ResponseStream.MoveNext())
                {
                    var invoice = call.ResponseStream.Current;
                    Console.WriteLine(invoice.ToString());
                    var lookup = client.LookupInvoice(paymentHash);
                    if (lookup.Settled)
                        break;
                }
            }
        }
        public string GetInfo()
        {
            return client.GetInfo(new GetInfoRequest()).ToString();
        }
        public string WalletBalance()
        {
            return client.WalletBalance(new WalletBalanceRequest()).ToString();
        }
    }
}
