import { apiClient } from './client';
import type {
  CommunityPostResponseDto,
  CommunityPostDetailDto,
  CommunityCommentResponseDto,
  PagedResultDto,
} from '@/types';

export const communityApi = {
  getPosts(page = 1, pageSize = 20) {
    return apiClient
      .get<PagedResultDto<CommunityPostResponseDto>>('/api/community/posts', { params: { page, pageSize } })
      .then((r) => r.data);
  },

  createPost(content: string, imageUrl?: string) {
    return apiClient
      .post<CommunityPostResponseDto>('/api/community/posts', { content, imageUrl })
      .then((r) => r.data);
  },

  getPost(postId: string) {
    return apiClient
      .get<CommunityPostDetailDto>(`/api/community/posts/${postId}`)
      .then((r) => r.data);
  },

  deletePost(postId: string) {
    return apiClient.delete(`/api/community/posts/${postId}`);
  },

  getComments(postId: string, page = 1, pageSize = 20) {
    return apiClient
      .get<CommunityCommentResponseDto[]>(`/api/community/posts/${postId}/comments`, { params: { page, pageSize } })
      .then((r) => r.data);
  },

  createComment(postId: string, content: string) {
    return apiClient
      .post<CommunityCommentResponseDto>(`/api/community/posts/${postId}/comments`, { content })
      .then((r) => r.data);
  },

  deleteComment(commentId: string) {
    return apiClient.delete(`/api/community/comments/${commentId}`);
  },
};
