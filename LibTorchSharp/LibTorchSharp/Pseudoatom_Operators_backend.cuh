﻿#include <cuda.h>
#include <cuda_runtime.h>
#include <torch/torch.h>
#include <c10/util/Exception.h>

#define CUDA_CHECK(EXPR)                                         \
  do {                                                               \
    cudaError_t __err = EXPR;                                        \
    if (__err != cudaSuccess) {                                      \
      auto error_unused C10_UNUSED = cudaGetLastError();             \
      std::cerr << "CUDA error: ", cudaGetErrorString(__err)); \
    }                                                                \
  } while (0)

#define CUDA_KERNEL_LAUNCH_CHECK() CUDA_CHECK(cudaGetLastError())

namespace at {
    namespace native {

        enum class GridSamplerInterpolation { Bilinear, Nearest, Bicubic };
        enum class GridSamplerPadding { Zeros, Border, Reflection };
        namespace MyOperator {
            std::tuple<Tensor, Tensor>
                 atoms_to_grid_3d_backward_cuda(const Tensor& grad_output, const Tensor& intensities, const Tensor& positions);

            Tensor atoms_to_grid_3d_cuda(const Tensor& intensities, const Tensor& positions, int64_t z, int64_t y, int64_t x);

            // volume is (N, D, H, W) layout; positions are (N, W, 3), orientations are matrixes (N, 3,3) dimensional, projections will be (N, y,x) dimensional
            Tensor projectAtoms(const Tensor& intensities, const Tensor& positions, const Tensor& orientation, int64_t z, int64_t y, int64_t x);

            std::tuple<Tensor, Tensor, Tensor>
                projectAtoms_backward_cuda(const Tensor& grad_output, const Tensor& intensities, const Tensor& positions, const Tensor& orientation, int64_t z, int64_t y, int64_t x);

            Tensor fft_crop(const Tensor& fft_volume, int3 newDims);
        }
    }
}

