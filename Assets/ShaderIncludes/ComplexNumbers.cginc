#if !defined(COMPLEX_NUMBERS)
#define COMPLEX_NUMBERS

float2 Conjugate(float2 a)
{
    return float2(a.x, -a.y);
}

// Complex multiplication, treating x as the real part and y as the imaginary part
float2 ComplexMult(float2 a, float2 b)
{
    float re = a.x * b.x - a.y * b.y;
    float im = a.x * b.y + a.y * b.x;
    return float2(re, im);
}

float2 ComplexDiv(float2 a, float2 b)
{
    return ComplexMult(a, Conjugate(b)) / length(b);
}

// Returns the value of exp(i * theta)
float2 ComplexExp(float theta)
{
    return float2(cos(theta), sin(theta));
}

// x = mag , y = arg
float2 GetPolarForm(float2 a)
{
    float2 output;
    output.x = length(a);
    output.y = atan(a.y/a.x);

    return output;
}

#endif