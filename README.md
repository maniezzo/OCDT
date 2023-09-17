# ODT
optimal decision tree

From linear k indices to upper triangular i-j indices
Given n (num rows/columns), i = 0, ..., n-1, j = i+1, ... , n-1, k linear index (starts at 0)
k = in - i(i-1)/2
2k = 2in - i^2 + i
i^2 - (2n+1)i + 2k = 0
i = 1/2( 2n+1 +- sqr( (2n+1)^2 - 8k ) )
i = 1/2( 2n+1 +- sqr( 4n^2 + 4n - 8k +1 ) )
j = k - ( in - i(i+1)/2 )

n = k(k-1)/2
2n = k^2 - k
k^2 - k - 2n = 0
k = 1/2( 1 +- sqr(1 + 8n) )
