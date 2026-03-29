using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

var sizes = new[] { (24, "Square44x44Logo.targetsize-24_altform-unplated.png"),
                    (88, "Square44x44Logo.scale-200.png"),
                    (50, "StoreLogo.png"),
                    (300, "Square150x150Logo.scale-200.png"),
                    (48, "LockScreenLogo.scale-200.png") };

var assetsPath = args[0];

foreach (var (size, name) in sizes)
{
    using var bmp = CreateLobster(size);
    bmp.Save(Path.Combine(assetsPath, name), ImageFormat.Png);
    Console.WriteLine("Created " + name);
}

static Bitmap CreateLobster(int size)
{
    var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);
    g.Clear(Color.Transparent);
    float s = size / 16f;
    
    var body = Color.FromArgb(255, 255, 79, 64);
    var claw = Color.FromArgb(255, 255, 119, 95);
    var outline = Color.FromArgb(255, 58, 10, 13);
    var eyeW = Color.FromArgb(255, 245, 251, 255);
    var eyeB = Color.FromArgb(255, 8, 16, 22);
    
    void P(int x, int y, Color c) { using var b = new SolidBrush(c); g.FillRectangle(b, (int)(x*s), (int)(y*s), (int)Math.Ceiling(s), (int)Math.Ceiling(s)); }
    
    int[][] bd = {new[]{5,3},new[]{6,3},new[]{7,3},new[]{8,3},new[]{9,3},new[]{10,3},new[]{4,4},new[]{5,4},new[]{7,4},new[]{8,4},new[]{10,4},new[]{11,4},new[]{3,5},new[]{4,5},new[]{5,5},new[]{7,5},new[]{8,5},new[]{10,5},new[]{11,5},new[]{12,5},new[]{3,6},new[]{4,6},new[]{5,6},new[]{6,6},new[]{7,6},new[]{8,6},new[]{9,6},new[]{10,6},new[]{11,6},new[]{12,6},new[]{3,7},new[]{4,7},new[]{5,7},new[]{6,7},new[]{7,7},new[]{8,7},new[]{9,7},new[]{10,7},new[]{11,7},new[]{12,7},new[]{4,8},new[]{5,8},new[]{6,8},new[]{7,8},new[]{8,8},new[]{9,8},new[]{10,8},new[]{11,8},new[]{5,9},new[]{6,9},new[]{7,9},new[]{8,9},new[]{9,9},new[]{10,9},new[]{5,12},new[]{6,12},new[]{7,12},new[]{8,12},new[]{9,12},new[]{10,12},new[]{6,13},new[]{7,13},new[]{8,13},new[]{9,13}};
    foreach (var p in bd) P(p[0], p[1], body);
    
    int[][] cl = {new[]{1,6},new[]{2,5},new[]{2,6},new[]{2,7},new[]{13,5},new[]{13,6},new[]{13,7},new[]{14,6}};
    foreach (var p in cl) P(p[0], p[1], claw);
    
    int[][] ol = {new[]{1,5},new[]{1,7},new[]{2,4},new[]{2,8},new[]{3,3},new[]{3,9},new[]{4,2},new[]{4,10},new[]{5,2},new[]{6,2},new[]{7,2},new[]{8,2},new[]{9,2},new[]{10,2},new[]{11,2},new[]{12,3},new[]{12,9},new[]{13,4},new[]{13,8},new[]{14,5},new[]{14,7},new[]{5,11},new[]{6,11},new[]{7,11},new[]{8,11},new[]{9,11},new[]{10,11},new[]{4,12},new[]{11,12},new[]{3,13},new[]{12,13},new[]{5,14},new[]{6,14},new[]{7,14},new[]{8,14},new[]{9,14},new[]{10,14}};
    foreach (var p in ol) P(p[0], p[1], outline);
    
    P(6,4,eyeW); P(9,4,eyeW); P(6,5,eyeB); P(9,5,eyeB);
    return bmp;
}
