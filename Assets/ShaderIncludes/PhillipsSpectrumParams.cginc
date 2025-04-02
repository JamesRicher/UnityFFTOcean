#if !defined(PHILLIPS_SPECTRUM_PARAMS)
#define PHILLIPS_SPECTRUM_PARAMS

struct PhillipsSpectrumParams
{
    float C; // Multiplicative constant
    float V; // Windspeed in m/s
    float2 omegaHat; // Normalised wind direction
    float Lx;
    float Lz;
    uint P;
    float l; // Small wave correction factor
    float L; // Largest possible wave arising from costant wind of speed V (L = V^2/g)
};

#endif