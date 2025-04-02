namespace OceanRendering
{
    public enum TextureResolution
    {
        Res32,
        Res64,
        Res128,
        Res256,
        Res512
    };

    static class TextureResolutionExtensions
    {
        public static int ToInt(this TextureResolution res)
        {
            switch(res)
            {
                case TextureResolution.Res32:
                    return 32;
                case TextureResolution.Res64:
                    return 64;
                case TextureResolution.Res128:
                    return 128;
                case TextureResolution.Res256:
                    return 256;
                case TextureResolution.Res512:
                    return 512;
                default:
                    return 0; // error
            }
        }
    }
}