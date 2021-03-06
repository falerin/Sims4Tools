﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using s4pi.Interfaces;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;

namespace s4pi.ImageResource
{
    public class ThumbnailResource : AResource
    {        
        const int recommendedApiVersion = 1;
        public override int RecommendedApiVersion { get { return recommendedApiVersion; } }
        public override List<string> ContentFields { get { return GetContentFields(requestedApiVersion, this.GetType()); } }

        private byte[] rawData;


        public ThumbnailResource(int APIversion, Stream s) : base(APIversion, s)  { Parse(s); }

        private void Parse(Stream s)
        {
            s.Position = 0;
            BinaryReader r = new BinaryReader(s);
            rawData = r.ReadBytes((int)s.Length);
            TransformToPNG();
        }

        protected override Stream UnParse()
        {
            return new MemoryStream(this.rawData);
        }

        public Stream ToImageStream()
        {
            MemoryStream ms = new MemoryStream();
            Image.Save(ms, ImageFormat.Png);
            ms.Position = 0;
            return ms;
        }

        private void TransformToPNG()
        {
            using (MemoryStream ms = new MemoryStream(this.rawData))
            {
                BinaryReader r = new BinaryReader(ms);
                Bitmap colorImage = new Bitmap(ms);
                ms.Position = 0;
                r.ReadBytes(24);
                if (r.ReadUInt32() == 0x41464C41U)
                {
                    int length = r.ReadInt32();
                    length = (int)((length & 0xFF000000) >> 24) | (int)((length & 0x00FF0000) >> 8) | (int)((length & 0x0000FF00) << 8) | (int)((length & 0x000000FF) << 24);
                    using (MemoryStream alphaStream = new MemoryStream(r.ReadBytes(length)))
                    {
                        Bitmap alphaImage = new Bitmap(alphaStream);
                        if (colorImage.Width != alphaImage.Width || colorImage.Height != alphaImage.Height) throw new InvalidDataException("Not a proper TS4 Thumbnail image");
                        //int[,] rawImage = new int[colorImage.Width, colorImage.Height];
                        //for (int y = 0; y < colorImage.Height; y++)
                        //{
                        //    for (int x = 0; x < colorImage.Width; x++)
                        //    {
                        //        Color color = colorImage.GetPixel(x, y);
                        //        byte alpha = alphaImage.GetPixel(x, y).R;
                        //        rawImage[x, y] = Color.FromArgb(alpha, color).ToArgb();

                        //    }
                        //}
                        colorImage = UpdateAlpha(colorImage, alphaImage);

                        this.Image = colorImage;
                    }
                }
                this.Image = colorImage;
            }
        }

        public Bitmap Image { get; private set; }

        protected internal unsafe Bitmap UpdateAlpha(Bitmap source, Bitmap alpha)
        {
            int width = source.Width;
            int height = source.Height;
            Bitmap img = new Bitmap(width, height);

            BitmapData imgBitmapData = img.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.ReadWrite,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb
            );

            BitmapData sourceBitmapData = source.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.ReadWrite,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb
            );

            BitmapData alphaBitmapData = alpha.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb
            );

            ColorARGB* imgStartingPosition = (ColorARGB*)imgBitmapData.Scan0;
            ColorARGB* sourceStartingPosition = (ColorARGB*)sourceBitmapData.Scan0;
            ColorARGB* alphaStartingPosition = (ColorARGB*)alphaBitmapData.Scan0;

            for (int i = 0; i < height; i++)
                for (int j = 0; j < width; j++)
                {
                    ColorARGB* sourcePosition = sourceStartingPosition + j + i * width;
                    ColorARGB* alphaPosition = alphaStartingPosition + j + i * width;
                    ColorARGB* imgPosition = imgStartingPosition + j + i * width;
                    *imgPosition = new ColorARGB(sourcePosition, alphaPosition);
                }

            img.UnlockBits(imgBitmapData);
            return img;
        }

        #region Sub-Type

        /// <summary>
        /// Basic ARGB color for Write into memory
        /// </summary>
        protected internal struct ColorARGB
        {
            public byte B;
            public byte G;
            public byte R;
            public byte A;
            public ColorARGB(Color color)
            {
                A = color.A;
                R = color.R;
                G = color.G;
                B = color.B;
            }

            public ColorARGB(ColorARGB color)
            {
                A = color.A;
                R = color.R;
                G = color.G;
                B = color.B;
            }

            public unsafe ColorARGB(ColorARGB* original, ColorARGB* alpha)
            {
                this.A = alpha->R;
                this.R = original->R;
                this.G = original->G;
                this.B = original->B;
            }

            public unsafe void UpdateAlha(ColorARGB* alpha)
            {
                this.A = alpha->R;
            }

            public ColorARGB(byte a, byte r, byte g, byte b)
            {
                A = a;
                R = r;
                G = g;
                B = b;
            }

            public ColorARGB(int color)
            {
                A = (byte)(color >> 24);
                R = (byte)((color & 0xFF0000) >> 16);
                G = (byte)((color & 0xFF00) >> 8);
                B = (byte)((color & 0XFF));
            }

            public Color ToColor()
            {
                return Color.FromArgb(A, R, G, B);
            }


        }
        #endregion
    }

    /// <summary>
    /// ResourceHandler for ThumbnailResource wrapper
    /// </summary>
    public class ThumbnailResourceHandler : AResourceHandler
    {
        public ThumbnailResourceHandler()
        {
            this.Add(typeof(ThumbnailResource), new List<string>(new string[] { "0x3C1AF1F2", "0xCD9DE247", "0x5B282D45", "0x0D338A3A", "0x3BD45407", "0x3C2A8647", }));
        }
    }
}
